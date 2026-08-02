using AwesomeAssertions;
using Domain.VolunteerOpportunities;

namespace Application.UnitTests.VolunteerOpportunities;

public class CheckInPinPolicyTests
{
	[Test]
	[Arguments("0000")]
	[Arguments("1111")]
	[Arguments("999999")]
	[Arguments("1234")]
	[Arguments("2345")]
	[Arguments("123456")]
	[Arguments("9876")]
	[Arguments("654321")]
	[Arguments("1212")]
	[Arguments("6969")]
	[Arguments("1313")]
	[Arguments("2001")]
	[Arguments("1010")]
	[Arguments("1998")]
	[Arguments("1004")]
	[Arguments("2000")]
	[Arguments("1999")]
	[Arguments("2580")]
	[Arguments("0852")]
	public void IsTrivial_ShouldReturnTrue_ForEasyToGuessPins(string pin)
	{
		CheckInPinPolicy.IsTrivial(pin).Should().BeTrue();
	}

	[Test]
	[Arguments("4827")]
	[Arguments("6193")]
	[Arguments("13579")]
	[Arguments("482170")]
	[Arguments("135790")]
	public void IsTrivial_ShouldReturnFalse_ForUnpredictablePins(string pin)
	{
		CheckInPinPolicy.IsTrivial(pin).Should().BeFalse();
	}
}
