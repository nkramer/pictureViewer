using System;
using System.Collections.Generic;

namespace Folio.Tests.TestHelpers {
    /// <summary>
    /// Helper class for testing IProgress&lt;T&gt; implementations.
    /// Captures all progress reports for verification in tests.
    /// </summary>
    public class ProgressCapture<T> : IProgress<T> {
        public List<T> Reports { get; } = new List<T>();

        public void Report(T value) {
            Reports.Add(value);
        }

        public void Clear() {
            Reports.Clear();
        }
    }
}
