using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CharacterEditor.Hair.EditorImport
{
    public sealed class J
    {
        public Dictionary<string, J> obj;
        public List<J> arr;
        public string str;
        public double num;
        public bool boolean;
        public bool isNull = true;

        public bool IsObj => obj != null;
        public bool IsArr => arr != null;
        public bool IsStr => str != null;
        public bool IsNum => !isNull && obj == null && arr == null && str == null;
        public int I => (int)num;
        public float F => (float)num;
        public string S => str ?? "";
        public bool B => boolean;

        public bool Has(string key) => obj != null && obj.ContainsKey(key);
        public J this[string key] => obj != null && obj.TryGetValue(key, out var v) ? v : Null();
        public J this[int index] => arr != null && index >= 0 && index < arr.Count ? arr[index] : Null();
        public int Count => arr != null ? arr.Count : obj != null ? obj.Count : 0;
        public static J Null() => new J();

        public static J Parse(string json) => new Parser(json).Parse();

        private sealed class Parser
        {
            private readonly string s;
            private int i;
            public Parser(string s) { this.s = s ?? ""; }

            public J Parse() { Skip(); return Value(); }
            private void Skip() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
            private char Peek => i < s.Length ? s[i] : '\0';
            private char Next() => i < s.Length ? s[i++] : '\0';

            private J Value()
            {
                Skip();
                char c = Peek;
                if (c == '{') return Obj();
                if (c == '[') return Arr();
                if (c == '"') return Str();
                if (c == 't' || c == 'f') return Bool();
                if (c == 'n') { i = Math.Min(i + 4, s.Length); return Null(); }
                return Num();
            }

            private J Obj()
            {
                var j = new J { isNull = false, obj = new Dictionary<string, J>() };
                Next(); Skip();
                if (Peek == '}') { Next(); return j; }
                while (i < s.Length)
                {
                    Skip(); var key = Str().S; Skip(); if (Peek == ':') Next();
                    j.obj[key] = Value(); Skip();
                    if (Peek == ',') { Next(); continue; }
                    if (Peek == '}') { Next(); break; }
                    break;
                }
                return j;
            }

            private J Arr()
            {
                var j = new J { isNull = false, arr = new List<J>() };
                Next(); Skip();
                if (Peek == ']') { Next(); return j; }
                while (i < s.Length)
                {
                    j.arr.Add(Value()); Skip();
                    if (Peek == ',') { Next(); continue; }
                    if (Peek == ']') { Next(); break; }
                    break;
                }
                return j;
            }

            private J Str()
            {
                var sb = new StringBuilder();
                if (Peek == '"') Next();
                while (i < s.Length)
                {
                    char c = Next();
                    if (c == '"') break;
                    if (c == '\\')
                    {
                        char e = Next();
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 <= s.Length && ushort.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                                { sb.Append((char)code); i += 4; }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }
                return new J { isNull = false, str = sb.ToString() };
            }

            private J Bool()
            {
                if (s.IndexOf("true", i, StringComparison.Ordinal) == i) { i += 4; return new J { isNull = false, boolean = true }; }
                i += 5; return new J { isNull = false, boolean = false };
            }

            private J Num()
            {
                int start = i;
                while (i < s.Length && "-+0123456789.eE".IndexOf(s[i]) >= 0) i++;
                double.TryParse(s.Substring(start, Math.Max(0, i - start)), NumberStyles.Float, CultureInfo.InvariantCulture, out var d);
                return new J { isNull = false, num = d };
            }
        }
    }
}
