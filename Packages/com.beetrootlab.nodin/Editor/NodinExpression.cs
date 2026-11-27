using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Nodin.Editor
{
    /// <summary>Resolves inspector member references and the supported expression grammar.</summary>
    public static class NodinExpression
    {
        internal const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic |
                                               BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        private static readonly ConcurrentDictionary<string, Func<object, object>> Expressions = new();
        private static readonly ConcurrentDictionary<(Type, string), Type> Types = new();
        private static readonly Dictionary<string, Type> Aliases = new()
        {
            ["string"] = typeof(string), ["bool"] = typeof(bool), ["int"] = typeof(int),
            ["float"] = typeof(float), ["double"] = typeof(double), ["object"] = typeof(object)
        };

        public static object Evaluate(object owner, string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new ArgumentException("A member name or expression is required.", nameof(reference));
            return Expressions.GetOrAdd(reference.TrimStart('@', '$'), text => new Parser(text).Parse())(owner);
        }

        public static bool TryEvaluate(object owner, string reference, out object value, out string error)
        {
            try
            {
                value = Evaluate(owner, reference);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                value = null;
                error = (exception.InnerException ?? exception).Message;
                return false;
            }
        }

        internal static FieldInfo FindField(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, Members);
                if (field != null)
                    return field;
            }
            return null;
        }

        internal static bool IsNull(object value) => value == null || value is UnityEngine.Object obj && !obj;

        internal static bool Truth(object value)
        {
            if (IsNull(value))
                return false;
            return value switch
            {
                bool flag => flag,
                string text => text.Length != 0,
                _ when IsNumber(value) => Number(value) != 0,
                _ => true
            };
        }

        private static bool IsNumber(object value) => value is byte or sbyte or short or ushort or int or uint
            or long or ulong or float or double or decimal or Enum;

        private static double Number(object value) => Convert.ToDouble(value, CultureInfo.InvariantCulture);

        internal static bool Equal(object left, object right)
        {
            if (IsNull(left) || IsNull(right))
                return IsNull(left) && IsNull(right);
            if (left is Enum && right is Enum && left.GetType() != right.GetType())
                return false;
            if (IsNumber(left) && IsNumber(right))
                return Number(left).Equals(Number(right));
            return Equals(left, right);
        }

        internal static object ConvertValue(object value, Type target)
        {
            if (value == null)
            {
                if (target.IsValueType && Nullable.GetUnderlyingType(target) == null)
                    throw new InvalidCastException($"Null cannot be assigned to {target.Name}.");
                return null;
            }
            if (target.IsInstanceOfType(value))
                return value;
            target = Nullable.GetUnderlyingType(target) ?? target;
            if (target.IsEnum)
                return value is string name ? Enum.Parse(target, name) : Enum.ToObject(target, value);
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        internal static object Call(object owner, string name, params object[] arguments)
        {
            var type = owner as Type ?? owner?.GetType() ?? throw new NullReferenceException(name);
            return Call(type, owner is Type ? null : owner, name, arguments);
        }

        private static object Call(Type type, object owner, string name, object[] arguments)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                MethodInfo selected = null;
                object[] values = null;
                var score = int.MaxValue;
                foreach (var method in current.GetMethods(Members))
                {
                    if (method.Name != name || method.ContainsGenericParameters || !method.IsStatic && owner == null)
                        continue;
                    var parameters = method.GetParameters();
                    if (arguments.Length > parameters.Length || parameters.Skip(arguments.Length).Any(p => !p.IsOptional)
                        || parameters.Any(p => p.ParameterType.IsByRef))
                        continue;

                    var candidates = new object[parameters.Length];
                    var cost = parameters.Length - arguments.Length;
                    try
                    {
                        for (var i = 0; i < parameters.Length; i++)
                        {
                            if (i >= arguments.Length)
                            {
                                candidates[i] = parameters[i].DefaultValue;
                                continue;
                            }

                            var target = parameters[i].ParameterType;
                            var argument = arguments[i];
                            candidates[i] = ConvertValue(argument, target);
                            if (argument?.GetType() != target)
                                cost += argument == null || target.IsInstanceOfType(argument) ? 1 : 4;
                        }
                    }
                    catch (Exception e) when (e is InvalidCastException or FormatException or OverflowException or ArgumentException)
                    {
                        continue;
                    }

                    if (cost >= score)
                        continue;
                    selected = method;
                    values = candidates;
                    score = cost;
                }

                if (selected != null)
                    return selected.Invoke(owner, values);
            }

            throw new MissingMethodException(type.FullName, name);
        }

        private struct Segment
        {
            internal string Name;
            internal Func<object, object>[] Arguments;
        }

        private static object Resolve(object owner, IReadOnlyList<Segment> path)
        {
            var scope = owner as Type ?? owner?.GetType() ?? throw new NullReferenceException("No expression owner.");
            var value = owner;
            var start = 0;
            if (path[0].Name == "this")
                start = 1;
            else if (!HasMember(scope, path[0].Name))
            {
                var prefix = "";
                for (var i = 0; i < path.Count && path[i].Arguments == null; i++)
                {
                    prefix += (i == 0 ? "" : ".") + path[i].Name;
                    var type = Types.GetOrAdd((scope, prefix), key => FindType(key.Item1, key.Item2));
                    if (type == null)
                        continue;
                    value = type;
                    start = i + 1;
                }
            }

            for (var i = start; i < path.Count; i++)
            {
                var segment = path[i];
                if (IsNull(value))
                    throw new NullReferenceException($"Cannot resolve '{segment.Name}' on a null value.");
                value = segment.Arguments == null
                    ? ReadMember(value, segment.Name)
                    : Call(value, segment.Name, segment.Arguments.Select(arg => arg(owner)).ToArray());
            }
            return value;
        }

        private static bool HasMember(Type type, string name)
        {
            for (; type != null; type = type.BaseType)
                if (type.GetMember(name, Members).Length > 0)
                    return true;
            return false;
        }

        private static Type FindType(Type scope, string name)
        {
            if (Aliases.TryGetValue(name, out var alias))
                return alias;

            for (var current = scope; current != null; current = current.BaseType)
            {
                var nested = current;
                foreach (var part in name.Split('.'))
                    nested = nested?.GetNestedType(part, BindingFlags.Public | BindingFlags.NonPublic);
                if (nested != null)
                    return nested;
            }

            foreach (var assembly in new[] { scope.Assembly }.Concat(AppDomain.CurrentDomain.GetAssemblies()))
            {
                var type = assembly.GetType(name) ??
                           (string.IsNullOrEmpty(scope.Namespace) ? null : assembly.GetType(scope.Namespace + "." + name)) ??
                           assembly.GetType("System." + name) ?? assembly.GetType("UnityEngine." + name);
                if (type != null)
                    return type;
            }
            return null;
        }

        private static object ReadMember(object owner, string name)
        {
            var type = owner as Type ?? owner.GetType();
            var instance = owner is Type ? null : owner;
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, Members);
                if (field != null && (field.IsStatic || instance != null))
                    return field.GetValue(instance);
                var property = current.GetProperties(Members)
                    .FirstOrDefault(p => p.Name == name && p.GetIndexParameters().Length == 0 && p.GetMethod != null
                        && (p.GetMethod.IsStatic || instance != null));
                if (property != null)
                    return property.GetValue(instance);
                var nested = current.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic);
                if (nested != null)
                    return nested;
            }
            return Call(type, instance, name, Array.Empty<object>());
        }

        private static object Binary(string token, object left, object right)
        {
            return token switch
            {
                "==" => Equal(left, right), "!=" => !Equal(left, right),
                ">" => Number(left) > Number(right), "<" => Number(left) < Number(right),
                ">=" => Number(left) >= Number(right), "<=" => Number(left) <= Number(right),
                "+" when left is string || right is string => string.Concat(left, right),
                "+" => Number(left) + Number(right), "-" => Number(left) - Number(right),
                "*" => Number(left) * Number(right), "/" => Number(left) / Number(right),
                "%" => Number(left) % Number(right),
                _ => throw new FormatException($"Unknown operator '{token}'.")
            };
        }

        private sealed class Parser
        {
            private static readonly Regex Lexer = new(
                @"\G\s*(?:""(?<string>(?:\\.|[^""\\])*)""|(?<number>\d+(?:\.\d+)?(?:[eE][+-]?\d+)?[fFdDmM]?)|(?<identifier>[\p{L}_][\p{L}\p{N}_]*)|(?<symbol>&&|\|\||==|!=|<=|>=|[+*/%!.<>()?,:\-]))",
                RegexOptions.CultureInvariant);
            private readonly string _text;
            private Match _token;
            private int _offset;
            private string Token => _token?.Groups["symbol"].Value;

            internal Parser(string text)
            {
                _text = text.Trim();
                Next();
            }

            internal Func<object, object> Parse()
            {
                var value = Conditional();
                if (_token != null)
                    throw Expected("end of expression");
                return value;
            }

            private void Next()
            {
                if (_offset == _text.Length)
                {
                    _token = null;
                    return;
                }
                _token = Lexer.Match(_text, _offset);
                if (!_token.Success)
                    throw Expected("valid expression token");
                _offset += _token.Length;
            }

            private FormatException Expected(string expected) =>
                new($"Expected {expected} near character {_offset} in '{_text}'.");

            private bool Take(string token)
            {
                if (Token != token)
                    return false;
                Next();
                return true;
            }

            private void Require(string token)
            {
                if (!Take(token))
                    throw Expected($"'{token}'");
            }

            private Func<object, object> Conditional()
            {
                var test = Operation(1);
                if (!Take("?"))
                    return test;
                var yes = Conditional();
                Require(":");
                var no = Conditional();
                return owner => Truth(test(owner)) ? yes(owner) : no(owner);
            }

            private static int Precedence(string token) => token switch
            {
                "||" => 1, "&&" => 2, "==" or "!=" => 3, ">" or ">=" or "<" or "<=" => 4,
                "+" or "-" => 5, "*" or "/" or "%" => 6, _ => 0
            };

            private Func<object, object> Operation(int precedence)
            {
                var result = Unary();
                while (Precedence(Token) >= precedence)
                {
                    var operation = Token;
                    var left = result;
                    Next();
                    var right = Operation(Precedence(operation) + 1);
                    result = operation switch
                    {
                        "&&" => owner => Truth(left(owner)) && Truth(right(owner)),
                        "||" => owner => Truth(left(owner)) || Truth(right(owner)),
                        _ => owner => Binary(operation, left(owner), right(owner))
                    };
                }
                return result;
            }

            private Func<object, object> Unary()
            {
                if (Take("!"))
                {
                    var operand = Unary();
                    return owner => !Truth(operand(owner));
                }
                if (Take("-"))
                {
                    var operand = Unary();
                    return owner => -Number(operand(owner));
                }
                if (Take("+"))
                    return Unary();
                return Primary();
            }

            private Func<object, object> Primary()
            {
                if (Take("("))
                {
                    var result = Conditional();
                    Require(")");
                    return result;
                }
                if (_token == null)
                    throw Expected("value");
                if (_token.Groups["string"].Success)
                {
                    var text = Regex.Unescape(_token.Groups["string"].Value);
                    Next();
                    return _ => text;
                }
                if (_token.Groups["number"].Success)
                {
                    var literal = _token.Groups["number"].Value;
                    object value = int.TryParse(literal, NumberStyles.None, CultureInfo.InvariantCulture, out var integer)
                        ? (object)integer : double.Parse(literal.TrimEnd('f', 'F', 'd', 'D', 'm', 'M'), CultureInfo.InvariantCulture);
                    Next();
                    return _ => value;
                }
                var name = _token.Groups["identifier"].Value;
                if (name is "true" or "false" or "null")
                {
                    Next();
                    return _ => name == "null" ? null : name == "true";
                }

                var segments = new List<Segment>();
                do
                {
                    name = _token?.Groups["identifier"].Value;
                    if (string.IsNullOrEmpty(name))
                        throw Expected("member name");
                    Next();
                    Func<object, object>[] arguments = null;
                    if (Take("("))
                    {
                        var values = new List<Func<object, object>>();
                        if (!Take(")"))
                        {
                            do { values.Add(Conditional()); } while (Take(","));
                            Require(")");
                        }
                        arguments = values.ToArray();
                    }
                    segments.Add(new Segment { Name = name, Arguments = arguments });
                } while (Take("."));

                return owner => Resolve(owner, segments);
            }
        }
    }
}
