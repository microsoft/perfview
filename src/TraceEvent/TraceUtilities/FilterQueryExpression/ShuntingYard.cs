using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
    /// <summary>
    /// Helper methods that implement a modified version of the Shunting Yard Algorithm 
    /// to convert an expression specified as a infix notation to postfix notation.
    /// </summary>
    internal static class ShuntingYard
    {
        private sealed class OperatorInfo
        {
            public OperatorInfo(char symbol, int precedence, bool rightAssociative)
            {
                Symbol = symbol;
                Precedence = precedence;
                RightAssociative = rightAssociative;
            }

            public char Symbol { get; }
            public int Precedence { get; }
            public bool RightAssociative { get; }
        }

        private static readonly Dictionary<char, OperatorInfo> operators = new Dictionary<char, OperatorInfo>
        {
            { '&', new OperatorInfo('&', 2, false) },
            { '|', new OperatorInfo('|', 1, false) }
        };
        private static readonly Regex _operandRegex = new Regex(@"-?[a-z]+");
        private static char[] Alphabets = Enumerable.Range('a', 26).Select(a => (char)a)
                                                    .ToArray();

        /// <summary>
        /// Method responsible for converting an infix expression into its postfix equivalent.
        /// </summary>
        /// <param name="infix"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static string ToPostFix(string infix)
        {
            var stack = new Stack<char>();
            var output = new List<char>();

            foreach (var token in infix)
            {
                // Check if the token is an operand. 
                if (char.IsLetter(token))
                {
                    output.Add(token);
                }

                // Check if the token is an operator and if the operator has lower precendence than that of the top of the stack,
                // pop out the old value and append it to the output to apply else, we get out of the loop and add the token to the stack.
                else if (operators.TryGetValue(token, out var operator1))
                {
                    while (stack.Count > 0 && operators.TryGetValue(stack.Peek(), out var operator2))
                    {
                        int operatorPrecedenceComparison = operator1.Precedence.CompareTo(operator2.Precedence);

                        // NOTE: Right associativity doesn't really matter for && and || but for the sake of completion, check it.
                        if (operatorPrecedenceComparison < 0 || !operator1.RightAssociative && operatorPrecedenceComparison <= 0)
                        {
                            output.Add(stack.Pop());
                        }

                        // Get out of this loop if we encounter an operator with higher precendence.
                        else
                        {
                            break;
                        }
                    }

                    stack.Push(token);
                }

                // Open parentheses have to be matched.
                else if (token == '(')
                {
                    stack.Push(token);
                }

                // Match the open parentheses and check if there are mismatches.
                else if (token == ')')
                {
                    char top = '\0';
                    while (stack.Count > 0 && (top = stack.Pop()) != '(')
                    {
                        output.Add(top);
                    }

                    if (top != '(')
                        throw new FilterQueryExpressionTreeParsingException("No matching left parentheses for expression.", infix);
                }
            }

            // Now that we have processed all the tokens, we add whatever is remaining in the operator stack into the output.
            while (stack.Count > 0)
            {
                var top = stack.Pop();

                // Mismatched Parentheses!
                if (!operators.ContainsKey(top)) 
                    throw new FilterQueryExpressionTreeParsingException("No matching right parentheses.", infix);

                output.Add(top);
            }

            return string.Join(" ", output);
        }

        /// <summary>
        /// Method that matches the postfix notation
        /// </summary>
        /// <param name="postfixNotation"></param>
        /// <param name="expressionMap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static bool Match(string postfixNotation, Dictionary<string, bool> expressionMap)
        {
            // Handle fast path. 
            if (postfixNotation.Length == 1)
            {
                return expressionMap.Values.First();
            }

            // Now that the representation of the expression is in postfix notation, it should be easy to deduce the logical expression.
            var tokens = new Stack<string>();
            string[] rawTokens = postfixNotation.Split(' ');
            foreach (var rawToken in rawTokens)
            {
                if (_operandRegex.IsMatch(rawToken))
                {
                    tokens.Push(rawToken);
                }

                else if (rawToken == "|" || rawToken == "&")
                {
                    var operand1 = tokens.Pop();
                    var operand2 = tokens.Pop();
                    var @operator = rawToken;
                    var result = EvaluateSingleExpression(operand1, operand2, @operator, expressionMap);
                    tokens.Push(result.ToString());
                }

                else
                {
                    throw new FilterQueryExpressionTreeParsingException($"Incorrect token encountered while parsing the postfix notation: {rawToken}", postfixNotation);
                }
            }

            if (tokens.Count > 0)
            {
                return bool.Parse(tokens.Pop());
            }

            throw new FilterQueryExpressionTreeMatchingException("Shouldn't get here. Check query", postfixNotation);
        }

        /// <summary>
        /// The evaluation of a single expression obtained from the PostFix deduction.
        /// </summary>
        /// <param name="operand1"></param>
        /// <param name="operand2"></param>
        /// <param name="operator"></param>
        /// <param name="expressionMap"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static bool EvaluateSingleExpression(string operand1, string operand2, string @operator, Dictionary<string, bool> expressionMap)
        {
            if (!expressionMap.TryGetValue(operand1, out bool deducedExpression1))
            {
                deducedExpression1 = bool.Parse(operand1);
            }

            if (!expressionMap.TryGetValue(operand2, out bool deducedExpression2))
            {
                deducedExpression2 = bool.Parse(operand2);
            }

            switch (@operator)
            {
                case "|":
                    return deducedExpression1 || deducedExpression2;
                case "&":
                    return deducedExpression1 && deducedExpression2;
                default:
                    throw new FilterQueryExpressionTreeMatchingException($"Operator: {@operator} not found for single expression.", $"{operand1} {@operator} {operand2}");
            }
        }

        /// <summary>
        /// Method responsible for priming the user specified expression in infix notation to convert into its modified postfix notation.
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="expressionMap"></param>
        /// <returns></returns>
        public static string PrimeExpression(string expression, out Dictionary<char, FilterQueryExpression> expressionMap)
        {
            var returnExpression = expression;

            // We want the operators as single characters for easier deduction.
            returnExpression = returnExpression.Replace("&&", "&").Replace("||", "|");

            // To create the expressionMap that consists an id which, is a lowercase alphabet, to the FilterQueryExpression,
            // we need to appropriately parse out the individual filter query expressions and this entails segregating them from the rest of the expression.
            expressionMap = new Dictionary<char, FilterQueryExpression>();
            expression = expression.Replace("&&", "`").Replace("||", "`");
            expression = expression.Replace("(", "").Replace(")", "");

            // Once the expression string is sufficiently primed to extract the individual FilterQueryExpressions, we create the expressionMap.
            var splitExpression = expression.Split('`');
            for (int i = 0; i < splitExpression.Length; i++)
            {
                // Constrain to 26 expressions.
                FilterQueryExpression fe = new FilterQueryExpression(splitExpression[i]);
                var alphabet = Alphabets[i];
                expressionMap[alphabet] = fe;
                returnExpression = returnExpression.Replace(splitExpression[i].TrimStart().TrimEnd(), alphabet.ToString());
            }

            return returnExpression;
        }
    }
}
