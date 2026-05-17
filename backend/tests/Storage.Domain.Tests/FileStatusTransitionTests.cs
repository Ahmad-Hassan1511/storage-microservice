using FluentAssertions;
using Storage.Domain.Common;
using Storage.Domain.Enums;
using DomainFile = Storage.Domain.Entities.File;

namespace Storage.Domain.Tests;

public class FileStatusTransitionTests
{
    private static DomainFile CreatePendingFile() =>
        DomainFile.Create(Guid.NewGuid(), "test-service", "documents", "test.pdf", "application/pdf", 1024);

    [Fact]
    public void Transition_FromPendingToScanning_Succeeds()
    {
        var file = CreatePendingFile();
        var act = () => file.Transition(FileStatus.Scanning);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(FileStatus.Ready)]
    [InlineData(FileStatus.Quarantined)]
    public void Transition_FromScanningToReadyOrQuarantined_Succeeds(FileStatus to)
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        var act = () => file.Transition(to);
        act.Should().NotThrow();
    }

    [Fact]
    public void Transition_FromReadyToDeleted_Succeeds()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        var act = () => file.Transition(FileStatus.Deleted);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(FileStatus.Ready)]
    [InlineData(FileStatus.Quarantined)]
    [InlineData(FileStatus.Deleted)]
    public void Transition_InvalidFromPending_ThrowsInvalidStatusTransitionException(FileStatus to)
    {
        var file = CreatePendingFile();
        var act = () => file.Transition(to);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromScanningToPending_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        var act = () => file.Transition(FileStatus.Pending);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromReadyToPending_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        var act = () => file.Transition(FileStatus.Pending);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromReadyToScanning_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        var act = () => file.Transition(FileStatus.Scanning);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Fact]
    public void Transition_FromQuarantinedToReady_Throws()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Quarantined);
        var act = () => file.Transition(FileStatus.Ready);
        act.Should().Throw<InvalidStatusTransitionException>();
    }

    [Theory]
    [InlineData(FileStatus.Scanning)]
    [InlineData(FileStatus.Ready)]
    [InlineData(FileStatus.Quarantined)]
    public void Transition_AfterValidTransition_StatusEqualsNewStatus(FileStatus to)
    {
        DomainFile file = CreatePendingFile();
        if (to == FileStatus.Ready || to == FileStatus.Quarantined)
        {
            file.Transition(FileStatus.Scanning);
        }
        file.Transition(to);
        file.Status.Should().Be(to);
    }

    [Fact]
    public void Transition_ReadyToDeleted_StatusEqualsDeleted()
    {
        var file = CreatePendingFile();
        file.Transition(FileStatus.Scanning);
        file.Transition(FileStatus.Ready);
        file.Transition(FileStatus.Deleted);
        file.Status.Should().Be(FileStatus.Deleted);
    }
}
