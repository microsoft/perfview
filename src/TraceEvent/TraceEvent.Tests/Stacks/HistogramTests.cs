using Microsoft.Diagnostics.Tracing.Stacks;
using Xunit;

namespace TraceEventTests.Stacks
{
    public class HistogramTests
    {
        /// <summary>
        /// Test for the bug where AddMetric doesn't properly transition from single-bucket to array mode.
        /// After creating the array, subsequent calls to AddMetric with the original bucket should update
        /// the array, not the m_singleBucketValue.
        /// </summary>
        [Fact]
        public void AddMetric_TransitionToArrayMode_OriginalBucketUpdatesArray()
        {
            // Create a simple histogram controller for testing
            var stackSource = new CopyStackSource();
            var callTree = new CallTree(ScalingPolicyKind.TimeMetric)
            {
                StackSource = stackSource
            };
            var controller = new TimeHistogramController(callTree, 0, 100);
            var histogram = new Histogram(controller);

            // Step 1: Add metric to bucket 0 (single bucket mode)
            histogram.AddMetric(1.0f, 0);
            Assert.Equal(1.0f, histogram[0]);

            // Step 2: Add metric to bucket 1 (should trigger array creation)
            histogram.AddMetric(2.0f, 1);
            Assert.Equal(1.0f, histogram[0]); // Bucket 0 should still have 1.0
            Assert.Equal(2.0f, histogram[1]); // Bucket 1 should have 2.0

            // Step 3: Add metric to bucket 0 again (this is where the bug manifests)
            // The value should be added to the array, not to m_singleBucketValue
            histogram.AddMetric(3.0f, 0);
            Assert.Equal(4.0f, histogram[0]); // Bucket 0 should now have 1.0 + 3.0 = 4.0
            Assert.Equal(2.0f, histogram[1]); // Bucket 1 should still have 2.0
        }

        /// <summary>
        /// Test that multiple buckets work correctly after transitioning to array mode.
        /// </summary>
        [Fact]
        public void AddMetric_MultipleBuckets_AllValuesCorrect()
        {
            var stackSource = new CopyStackSource();
            var callTree = new CallTree(ScalingPolicyKind.TimeMetric)
            {
                StackSource = stackSource
            };
            var controller = new TimeHistogramController(callTree, 0, 100);
            var histogram = new Histogram(controller);

            // Add to multiple buckets in various order
            histogram.AddMetric(10.0f, 5);
            histogram.AddMetric(20.0f, 10);
            histogram.AddMetric(30.0f, 5);  // Add more to bucket 5
            histogram.AddMetric(40.0f, 15);
            histogram.AddMetric(50.0f, 10); // Add more to bucket 10

            // Verify all values are correct
            Assert.Equal(0.0f, histogram[0]);
            Assert.Equal(40.0f, histogram[5]);  // 10 + 30
            Assert.Equal(70.0f, histogram[10]); // 20 + 50
            Assert.Equal(40.0f, histogram[15]);
        }

        /// <summary>
        /// Test single bucket mode (when only one bucket is ever used).
        /// </summary>
        [Fact]
        public void AddMetric_SingleBucketOnly_ValuesCorrect()
        {
            var stackSource = new CopyStackSource();
            var callTree = new CallTree(ScalingPolicyKind.TimeMetric)
            {
                StackSource = stackSource
            };
            var controller = new TimeHistogramController(callTree, 0, 100);
            var histogram = new Histogram(controller);

            // Add multiple values to the same bucket
            histogram.AddMetric(1.0f, 7);
            histogram.AddMetric(2.0f, 7);
            histogram.AddMetric(3.0f, 7);

            // Verify the value accumulated correctly
            Assert.Equal(6.0f, histogram[7]);
            Assert.Equal(0.0f, histogram[0]);
            Assert.Equal(0.0f, histogram[10]);
        }

        /// <summary>
        /// Test AddScaled works correctly after histogram transition to array mode.
        /// </summary>
        [Fact]
        public void AddScaled_AfterArrayTransition_ValuesCorrect()
        {
            var stackSource = new CopyStackSource();
            var callTree = new CallTree(ScalingPolicyKind.TimeMetric)
            {
                StackSource = stackSource
            };
            var controller = new TimeHistogramController(callTree, 0, 100);
            
            // Create source histogram with multiple buckets
            var sourceHistogram = new Histogram(controller);
            sourceHistogram.AddMetric(10.0f, 5);
            sourceHistogram.AddMetric(20.0f, 10);
            
            // Create target histogram that starts in single-bucket mode
            var targetHistogram = new Histogram(controller);
            targetHistogram.AddMetric(5.0f, 5);
            
            // Add scaled source to target - this should transition target to array mode
            targetHistogram.AddScaled(sourceHistogram);
            
            // Verify values
            Assert.Equal(15.0f, targetHistogram[5]);  // 5 + 10
            Assert.Equal(20.0f, targetHistogram[10]); // 0 + 20
        }
    }
}
