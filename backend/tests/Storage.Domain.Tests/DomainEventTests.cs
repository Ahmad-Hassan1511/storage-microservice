using FluentAssertions;
using Storage.Domain.Events;

namespace Storage.Domain.Tests;

public class DomainEventTests
{
    private static readonly Guid TestFileId = Guid.NewGuid();
    private static readonly Guid TestTenantId = Guid.NewGuid();
    private const string TestOwnerService = "test-service";

    [Fact]
    public void FileCreatedEvent_IsInstantiable_WithRequiredFields()
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new FileCreatedEvent(TestFileId, TestTenantId, TestOwnerService, "documents", eventId, occurredAt);

        evt.FileId.Should().Be(TestFileId);
        evt.TenantId.Should().Be(TestTenantId);
        evt.EventId.Should().Be(eventId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().Be(occurredAt);
        evt.OccurredAt.Should().NotBe(default);
    }

    [Fact]
    public void FileUploadedEvent_IsInstantiable_WithRequiredFields()
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new FileUploadedEvent(TestFileId, TestTenantId, TestOwnerService, eventId, occurredAt);

        evt.FileId.Should().Be(TestFileId);
        evt.TenantId.Should().Be(TestTenantId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().NotBe(default);
    }

    [Fact]
    public void FileScannedEvent_IsInstantiable_WithRequiredFields()
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new FileScannedEvent(TestFileId, TestTenantId, TestOwnerService, false, eventId, occurredAt);

        evt.FileId.Should().Be(TestFileId);
        evt.TenantId.Should().Be(TestTenantId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().NotBe(default);
    }

    [Fact]
    public void FileReadyEvent_IsInstantiable_WithRequiredFields()
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new FileReadyEvent(TestFileId, TestTenantId, TestOwnerService, eventId, occurredAt);

        evt.FileId.Should().Be(TestFileId);
        evt.TenantId.Should().Be(TestTenantId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().NotBe(default);
    }

    [Fact]
    public void FileDeletedEvent_IsInstantiable_WithRequiredFields()
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new FileDeletedEvent(TestFileId, TestTenantId, TestOwnerService, eventId, occurredAt);

        evt.FileId.Should().Be(TestFileId);
        evt.TenantId.Should().Be(TestTenantId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().NotBe(default);
    }

    [Fact]
    public void FilePermissionChangedEvent_IsInstantiable_WithRequiredFields()
    {
        var eventId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var evt = new FilePermissionChangedEvent(TestFileId, TestTenantId, TestOwnerService, "user", "principal-1", eventId, occurredAt);

        evt.FileId.Should().Be(TestFileId);
        evt.TenantId.Should().Be(TestTenantId);
        evt.EventId.Should().NotBeEmpty();
        evt.OccurredAt.Should().NotBe(default);
    }
}
