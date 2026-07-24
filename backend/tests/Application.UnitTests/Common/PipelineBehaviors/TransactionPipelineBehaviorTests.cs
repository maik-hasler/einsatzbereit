using Application.Common.Messaging;
using Application.Common.PipelineBehaviors;
using Application.Common.Persistence;
using AwesomeAssertions;
using NSubstitute;


namespace Application.UnitTests.Common.PipelineBehaviors;

public class TransactionPipelineBehaviorTests
{
	[Test]
	public async Task Handle_ShouldBeginSaveAndCommit_WhenNoTransactionIsActive(
		CancellationToken cancellationToken)
	{
		// Arrange
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.HasActiveTransaction.Returns(false);

		var behavior = new TransactionPipelineBehavior<TestCommand, string>(unitOfWork);

		// Act
		var result = await behavior.Handle(new TestCommand(), () => ValueTask.FromResult("ok"), cancellationToken);

		// Assert
		result.Should().Be("ok");
		await unitOfWork.Received(1).BeginTransactionAsync(cancellationToken);
		await unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
		await unitOfWork.Received(1).CommitTransactionAsync(cancellationToken);
		await unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldRollback_WhenNoTransactionIsActiveAndNextThrows(
		CancellationToken cancellationToken)
	{
		// Arrange
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.HasActiveTransaction.Returns(false);

		var behavior = new TransactionPipelineBehavior<TestCommand, string>(unitOfWork);

		ValueTask<string> Next() => throw new InvalidOperationException("boom");

		// Act
		Func<Task> act = async () => await behavior.Handle(new TestCommand(), Next, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>();
		await unitOfWork.Received(1).BeginTransactionAsync(cancellationToken);
		await unitOfWork.Received(1).RollbackTransactionAsync(cancellationToken);
		await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
		await unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSkipBeginSaveAndCommit_WhenATransactionIsAlreadyActive(
		CancellationToken cancellationToken)
	{
		// Arrange - simulates a nested Send() call (e.g. AwardAchievementCommand
		// dispatched from ConfirmEngagementCommandHandler) sharing the outer
		// command's IUnitOfWork/transaction via Sender's ambient scope reuse.
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.HasActiveTransaction.Returns(true);

		var behavior = new TransactionPipelineBehavior<TestCommand, string>(unitOfWork);

		// Act
		var result = await behavior.Handle(new TestCommand(), () => ValueTask.FromResult("nested-ok"), cancellationToken);

		// Assert - the outermost command owns begin/save/commit/rollback; this
		// nested invocation must not touch any of them.
		result.Should().Be("nested-ok");
		await unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
		await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
		await unitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
		await unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSwallowRollback_WhenATransactionIsAlreadyActiveAndNextThrows(
		CancellationToken cancellationToken)
	{
		// Arrange - a nested command's own failure must still propagate so the
		// outer command's TransactionPipelineBehavior can roll everything back.
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.HasActiveTransaction.Returns(true);

		var behavior = new TransactionPipelineBehavior<TestCommand, string>(unitOfWork);

		ValueTask<string> Next() => throw new InvalidOperationException("nested boom");

		// Act
		Func<Task> act = async () => await behavior.Handle(new TestCommand(), Next, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("nested boom");
		await unitOfWork.DidNotReceive().RollbackTransactionAsync(Arg.Any<CancellationToken>());
	}

	private sealed record TestCommand : ICommand<string>;
}
