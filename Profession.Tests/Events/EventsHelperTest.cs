using System.Collections.Generic;
using System.Threading.Tasks;
using AgendaBuddy.Library.Entities;
using AgendaBuddy.Library.Repositories;
using AgendaBuddy.Library.Services;
using MediatR;
using Profession.Events;
using Profession.Requests;

namespace Profession.Tests.Events;

[TestSubject(typeof(EventsHelper))]
public class EventsHelperTest
{
    private readonly Mock<IRequestCollection> _mockRequestCollection;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ProfessionService> _mockProfessionService;
    private readonly ProfessionEntity _professionEntity;

    public EventsHelperTest()
    {
        _mockRequestCollection = new Mock<IRequestCollection>();
        _mockMediator = new Mock<IMediator>();
        var mockRepositoryProfession = new Mock<IRepository<ProfessionEntity>>();
        _mockProfessionService = new Mock<ProfessionService>(mockRepositoryProfession.Object);
        _professionEntity = new ProfessionEntity
        {
            Id = default,
            Name = "AnyName"
        };
    }

    // ── AddProfessionEvent_ReturnSuccess was REMOVED by F-016-T17 ────────────────────────────────
    //
    // ⚠️ This is the one pre-existing test F-016 deletes, and PRD AC-19 says "no pre-existing test was
    // deleted or skipped to achieve this". Flagged rather than done quietly, because the distinction
    // matters and only a human can accept it:
    //
    //   * AC-19 exists to stop a test being deleted BECAUSE IT FAILED -- deleting evidence to make a
    //     change look green.
    //   * This test's SUBJECT was deliberately removed. ADR-025 deletes POST /api/v1/professions and its
    //     EventsHelper/RequestCollection write path, so EventsHelper.AddProfessionEvent no longer exists
    //     and the test cannot compile, let alone pass. Keeping it would mean keeping the write path this
    //     task exists to remove.
    //
    // The requirement is not lost, which is the property that actually matters. It is INVERTED and pinned
    // harder than before: AgendaBuddy.IntegrationTests ProfessionWriteRouteRemovedTest asserts over real
    // HTTP that the route returns 404/405 for BOTH roles and that no profession is written, and that the
    // two read routes still return 200 anonymously. Net change: -1 unit test, +3 integration tests.
    //
    // Recorded for F-016-T19's attestation and in STATE. The two tests below are untouched.

    [Fact]
    public async Task GetProfessionByNameEvent_ReturnSuccess()
    {
        // Arrange
        var expectedResponse = new ProfessionEntity
        {
            Name = "A profession name"
        };
        _mockRequestCollection.Setup(rc =>
                rc.GetProfessionByNameRequest(It.IsAny<IMediator>(), It.IsAny<ProfessionService>(),
                    It.IsAny<string>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.GetProfessionByNameEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockProfessionService.Object, "AnyName");

        // Assert
        Assert.Equal(expectedResponse, result);
    }

    [Fact]
    public async Task GetProfessionsEvent_ReturnSuccess()
    {
        // Arrange
        var expectedResponse = new List<ProfessionEntity>
        {
            new ProfessionEntity
            {
                Name = "A profession name",
            }
        };
        _mockRequestCollection.Setup(rc =>
                rc.GetProfessionsRequest(It.IsAny<IMediator>(), It.IsAny<ProfessionService>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await EventsHelper.GetAllProfessionsEvent(_mockRequestCollection.Object, _mockMediator.Object,
            _mockProfessionService.Object);

        // Assert
        Assert.Equal(expectedResponse, result);
    }
}
