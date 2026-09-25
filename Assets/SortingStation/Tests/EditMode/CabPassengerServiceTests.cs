using NUnit.Framework;

namespace SortingStation.Tests
{
    public sealed class CabPassengerServiceTests
    {
        [Test]
        public void SameSeedProducesTheSameStationReport()
        {
            CabPassengerService first = new CabPassengerService(813, 76);
            CabPassengerService second = new CabPassengerService(813, 76);
            CabStationDefinition station = CabStationNetwork.AtSequence(1);

            CabPassengerStopReport a = first.CallAt(station);
            CabPassengerStopReport b = second.CallAt(station);

            Assert.That(a.Boarded, Is.EqualTo(b.Boarded));
            Assert.That(a.Alighted, Is.EqualTo(b.Alighted));
            Assert.That(a.Onboard, Is.EqualTo(b.Onboard));
            CollectionAssert.AreEqual(a.BoardedDestinations, b.BoardedDestinations);
        }

        [Test]
        public void PassengerCountNeverExceedsCapacity()
        {
            CabPassengerService service = new CabPassengerService(11, 20);
            for (int i = 0; i < 30; i++)
            {
                CabPassengerStopReport report = service.CallAt(CabStationNetwork.AtSequence(i));
                Assert.That(report.Onboard, Is.InRange(0, report.Capacity));
            }
        }

        [Test]
        public void NetworkContainsBothRequestedDirections()
        {
            Assert.That(CabStationNetwork.AtSequence(5).DisplayName, Is.EqualTo("Выборг"));
            Assert.That(CabStationNetwork.AtSequence(10).DisplayName, Is.EqualTo("Приозерск"));
        }
    }
}
