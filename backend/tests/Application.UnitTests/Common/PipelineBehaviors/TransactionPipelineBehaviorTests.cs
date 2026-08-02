using Application.Common.Messaging;
using Application.Common.PipelineBehaviors;
using Application.Common.Persistence;
using AwesomeAssertions;
using NSubstitute;


namespace Application.UnitTests.Common.PipelineBehaviors;

public class TransactionPipelineBehaviorTests
{
	[Test]
	public async Task Handle_ShouldExecuteInTransactionAndSaveChanges_WhenNoTransactionIsActive(
		CancellationToken cancellationToken)
	{
		// Arrange
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.HasActiveTransaction.Returns(false);
		unitOfWork
			.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<string>>>(), cancellationToken)
			.Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task<string>>>()!(cancellationToken));

		var behavior = new TransactionPipelineBehavior<TestCommand, string>(unitOfWork);

		// Act
		var result = await behavior.Handle(new TestCommand(), () => ValueTask.FromResult("ok"), cancellationToken);

		// Assert - begin/commit-or-rollback is ApplicationDbContext's own
		// responsibility now (ExecuteInTransactionAsync, wrapped in
		// CreateExecutionStrategy for EnableRetryOnFailure); this only verifies
		// the behavior runs the operation through it and saves afterwards.
		result.Should().Be("ok");
		await unitOfWork.Received(1).ExecuteInTransactionAsync(
			Arg.Any<Func<CancellationToken, Task<string>>>(), cancellationToken);
		await unitOfWork.Received(1).SaveChangesAsync(cancellationToken);
	}

	[Test]
	public async Task Handle_ShouldPropagateExceptionWithoutSaving_WhenNoTransactionIsActiveAndNextThrows(
		CancellationToken cancellationToken)
	{
		// Arrange
		var unitOfWork = Substitute.For<IUnitOfWork>();
		unitOfWork.HasActiveTransaction.Returns(false);
		unitOfWork
			.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task<string>>>(), cancellationToken)
			.Returns(callInfo => callInfo.Arg<Func<CancellationToken, Task<string>>>()!(cancellationToken));

		var behavior = new TransactionPipelineBehavior<TestCommand, string>(unitOfWork);

		ValueTask<string> Next() => throw new InvalidOperationException("boom");

		// Act
		Func<Task> act = async () => await behavior.Handle(new TestCommand(), Next, cancellationToken);

		// Assert
		await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
		await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldSkipTransactionAndSaveChanges_WhenATransactionIsAlreadyActive(
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

		// Assert - the outermost command owns the single transaction/save; this
		// nested invocation must not touch either.
		result.Should().Be("nested-ok");
		await unitOfWork.DidNotReceive().ExecuteInTransactionAsync(
			Arg.Any<Func<CancellationToken, Task<string>>>(), Arg.Any<CancellationToken>());
		await unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Handle_ShouldNotSwallowException_WhenATransactionIsAlreadyActiveAndNextThrows(
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
	}

	private sealed record TestCommand : ICommand<string>;
}
