using System.Collections.Generic;
using NUnit.Framework;
using PhysicsQueries;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CharacterShowcase.Tests.EditMode
{
    /// <summary>
    /// Tests of the growing physics query buffers (PhysicsQueries.QueryBuffer): a query that fills its buffer grows it
    /// and runs again, so every result is found (and with them the nearest, which Unity's unordered results could leave
    /// out), reporting it; and a buffer stops growing at its largest, reporting that once.
    /// </summary>
    [Category(TestArea.Movement)]
    [TestTiming]
    public class QueryBufferTests
    {
        private readonly List<GameObject> made = new();

        // The warnings logged while a test runs (a buffer's reports).
        private readonly List<string> warnings = new();

        private void Record(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Warning) { warnings.Add(message); }
        }

        [SetUp]
        public void ListenForWarnings() => Application.logMessageReceived += Record;

        [TearDown]
        public void DestroyMade()
        {
            Application.logMessageReceived -= Record;
            warnings.Clear();
            foreach (GameObject go in made) { if (go != null) { Object.DestroyImmediate(go); } }
            made.Clear();
        }

        /// <summary>Boxes (half a metre) in a row along +Z from z = 1, a metre apart, far from anything else in the scene.</summary>
        private Vector3 Row(int count)
        {
            Vector3 start = new Vector3(5000f, 5000f, 5000f);
            for (int i = 0; i < count; i++)
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = $"Query Buffer Box {i}";
                box.transform.position = start + Vector3.forward * (1f + i);
                box.transform.localScale = Vector3.one * 0.5f;
                made.Add(box);
            }
            Physics.SyncTransforms();
            return start;
        }

        [Test]
        public void ARayThatFillsItsBuffer_GrowsIt_AndFindsEveryHit_TheNearestToo()
        {
            Vector3 start = Row(20);
            var hits = new RaycastHitBuffer(4, "Test.hits");
            int count = hits.Raycast(start, Vector3.forward, 100f, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(count, Is.EqualTo(20), "Every box the ray passes through should be found.");
            Assert.That(hits.Capacity, Is.EqualTo(32), "The buffer should have doubled until the hits fit.");
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++) { nearest = Mathf.Min(nearest, hits[i].distance); }
            Assert.That(nearest, Is.EqualTo(0.75f).Within(1e-3f), "The nearest box should be among them.");
            Assert.That(warnings, Has.Count.EqualTo(3).And.All.Contains("[Test.hits] A query filled its buffer"),
                "Each time it grew (4 to 8, 16 and 32) should be reported.");
            // Grown, it stays grown: the same query fits, so isn't reported again.
            Assert.That(hits.Raycast(start, Vector3.forward, 100f, ~0, QueryTriggerInteraction.Ignore), Is.EqualTo(20));
            Assert.That(warnings, Has.Count.EqualTo(3), "A query that fits shouldn't be reported.");
        }

        [Test]
        public void AnOverlapThatFillsItsBuffer_GrowsIt_AndFindsEveryCollider()
        {
            Vector3 start = Row(10);
            var overlaps = new ColliderBuffer(2, "Test.overlaps");
            int count = overlaps.OverlapBox(start + Vector3.forward * 5.5f, new Vector3(1f, 1f, 6f), Quaternion.identity, ~0,
                QueryTriggerInteraction.Ignore);
            Assert.That(count, Is.EqualTo(10), "Every box should be found.");
            Assert.That(overlaps.Capacity, Is.EqualTo(16));
            Assert.That(warnings, Has.Count.EqualTo(3).And.All.Contains("[Test.overlaps] A query filled its buffer"));
        }

        [Test]
        public void ABufferAtItsLargest_DoesntGrow_AndReportsItOnce()
        {
            int boxes = QueryBuffer<RaycastHit>.MaxSize + 10;
            Vector3 start = Row(boxes);
            var hits = new RaycastHitBuffer(QueryBuffer<RaycastHit>.MaxSize, "Test.full");
            int count = hits.Raycast(start, Vector3.forward, boxes + 10f, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(count, Is.EqualTo(QueryBuffer<RaycastHit>.MaxSize), "At its largest, it should hold as many as it can.");
            Assert.That(hits.Capacity, Is.EqualTo(QueryBuffer<RaycastHit>.MaxSize), "It shouldn't grow past its largest.");
            // Reported once, not every query.
            hits.Raycast(start, Vector3.forward, boxes + 10f, ~0, QueryTriggerInteraction.Ignore);
            Assert.That(warnings, Has.Count.EqualTo(1).And.All.Contains("[Test.full] A query filled its buffer at its largest"),
                "Full at its largest should be reported once, not every query.");
        }
    }
}
