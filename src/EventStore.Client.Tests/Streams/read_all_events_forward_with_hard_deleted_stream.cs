using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class read_all_events_forward_with_hard_deleted_stream
		: IClassFixture<read_all_events_forward_with_hard_deleted_stream.Fixture> {
		private const string Stream = "stream";
		private readonly Fixture _fixture;

		public read_all_events_forward_with_hard_deleted_stream(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task reading_hard_deleted_stream_throws_stream_deleted() {
			var ex = await Assert.ThrowsAsync<StreamDeletedException>(() =>
				_fixture.Client.ReadStreamAsync(Direction.Forwards, Stream, StreamRevision.Start, 1).CountAsync().AsTask());
			Assert.Equal(Stream, ex.Stream);
		}

		[Fact]
		public async Task returns_all_events_including_tombstone() {
			var events = await _fixture.Client.ReadAllAsync(Direction.Forwards, Position.Start, (ulong)_fixture.Events.Length * 2)
				.ToArrayAsync();

			var tombstone = events.Last();
			Assert.Equal(Stream, tombstone.Event.EventStreamId);
			Assert.Equal(SystemEventTypes.StreamDeleted, tombstone.Event.EventType);

			Assert.True(EventDataComparer.Equal(_fixture.Events, events.AsResolvedTestEvents().ToArray()));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override async Task Given() {
				var result = await Client.SetStreamMetadataAsync(
					"$all",
					AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(readRole: "$all")),
					userCredentials: TestCredentials.Root);
				Events = CreateTestEvents(20).ToArray();

				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, Events);
				await Client.TombstoneAsync(Stream, AnyStreamRevision.Any);
			}

			public EventData[] Events { get; private set; }

			protected override Task When() => Task.CompletedTask;
		}
	}
}
