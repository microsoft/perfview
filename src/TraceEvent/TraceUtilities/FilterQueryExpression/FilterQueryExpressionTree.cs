using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    public sealed class FilterQueryExpressionTree
    {
        private readonly FilterQueryExpression _simpleFilterQueryExpression;
        private readonly Dictionary<char, FilterQueryExpression> _expressionMap;
        private readonly string _postFixExpression;

        public FilterQueryExpressionTree(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new ArgumentException($"{nameof(expression)} is null. Invalid Filter Expression Tree.");
            }

            // Base Case: Simple Expression Without Parentheses.
            if (FilterQueryExpression.IsValidExpression(expression) &&
                (!expression.Contains("(") || expression.Contains(")")))
            {
                _simpleFilterQueryExpression = new FilterQueryExpression(expression);
            }

            else
            {
                OriginalExpression = expression;

                // Prime Expression -> Post Fix
                string primedExpression = ShuntingYard.PrimeExpression(OriginalExpression, out var _expressionMap);
                _postFixExpression = ShuntingYard.ToPostFix(primedExpression);
            }
        }

        public string OriginalExpression { get; }

        public bool Match(TraceEvent @event)
        {
            if (_simpleFilterQueryExpression != null)
            {
                return _simpleFilterQueryExpression.Match(@event);
            }

            Dictionary<string, bool> convertedExpressionMap = new Dictionary<string, bool>();
            foreach (var kvp in _expressionMap)
            {
                convertedExpressionMap[kvp.Key.ToString()] = kvp.Value.Match(@event);
            }

            return ShuntingYard.Match(_postFixExpression, convertedExpressionMap);
        }
    }
}
