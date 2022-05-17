using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression
{
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

        public static string ToPostFix(this string infix)
        {
            var stack = new Stack<char>();
            var output = new List<char>();

            foreach (var token in infix)
            {
                if (char.IsLetter(token))
                {
                    output.Add(token);
                }

                if (operators.TryGetValue(token, out var opt1))
                {
                    while (stack.Count > 0 && operators.TryGetValue(stack.Peek(), out var op2))
                    {
                        int c = opt1.Precedence.CompareTo(op2.Precedence);
                        if (c < 0 || !opt1.RightAssociative && c <= 0)
                        {
                            output.Add(stack.Pop());
                        }
                        else
                        {
                            break;
                        }
                    }

                    stack.Push(token);
                }

                else if (token == '(')
                {
                    stack.Push(token);
                }

                else if (token == ')')
                {
                    char top = '\0';
                    while (stack.Count > 0 && (top = stack.Pop()) != '(')
                    {
                        output.Add(top);
                    }

                    if (top != '(')
                        throw new ArgumentException("No matching left parentheses.");
                }
            }

            while (stack.Count > 0)
            {
                var top = stack.Pop();
                if (!operators.ContainsKey(top)) throw new ArgumentException("No matching right parentheses");
                output.Add(top);
            }

            return String.Join(" ", output);
        }

        private static readonly Regex _operandRegex = new Regex(@"-?[a-z]+");

        public static bool Match(string postfixNotation, Dictionary<string, bool> expressionMap)
        {
            // Handle fast path.
            if (postfixNotation.Length == 1)
            {
                return expressionMap["a"];
            }

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
            }

            if (tokens.Count > 0)
            {
                return bool.Parse(tokens.Pop());
            }

            throw new ArgumentException("Shouldn't get here!");
        }

        public static bool EvaluateSingleExpression(string operand1, string operand2, string @operator, Dictionary<string, bool> expressionMap)
        {
            var exp1 = expressionMap.ContainsKey(operand1) ? expressionMap[operand1] : bool.Parse(operand1);
            var exp2 = expressionMap.ContainsKey(operand2) ? expressionMap[operand2] : bool.Parse(operand2);

            switch (@operator)
            {
                case "|":
                    return exp1 || exp2;
                case "&":
                    return exp1;
                default:
                    throw new ArgumentException($"Operator: {@operator} not found.");
            }
        }

        private static char[] Alphabets = Enumerable.Range('a', 26).Select(a => (char)a)
                                                    .Concat(Enumerable.Range('A', 26).Select(a => (char)a))
                                                    .ToArray();
        public static string PrimeExpression(string expression, out Dictionary<char, FilterQueryExpression> expressionMap)
        {
            var returnExpression = expression;
            returnExpression = returnExpression.Replace("&&", "&").Replace("||", "|");

            expressionMap = new Dictionary<char, FilterQueryExpression>();
            expression = expression.Replace("&&", "`").Replace("||", "`");
            expression = expression.Replace("(", "").Replace(")", "");

            var splitExpression = expression.Split('`');
            for (int i = 0; i < splitExpression.Length; i++)
            {
                // Constrain to 52 expressions.
                FilterQueryExpression fe = new FilterQueryExpression(splitExpression[i]);
                var alphabet = Alphabets[i];
                expressionMap[alphabet] = fe;
                returnExpression = returnExpression.Replace(splitExpression[i].TrimStart().TrimEnd(), alphabet.ToString());
            }

            return returnExpression;
        }
    }
}
