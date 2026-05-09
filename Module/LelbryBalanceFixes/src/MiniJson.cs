using System.Collections.Generic;
using System.Text;

namespace LelbryBalanceFixes
{
    /// <summary>
    /// Minimal JSON parser for a flat string array like ["id1","id2"].
    /// Avoids pulling Newtonsoft/System.Text.Json into the mod (keeps deps slim).
    /// </summary>
    internal static class MiniJson
    {
        public static List<string> ParseStringArray(string json)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(json)) return result;

            int i = 0;
            SkipWs(json, ref i);
            if (i >= json.Length || json[i] != '[') return result;
            i++;

            while (i < json.Length)
            {
                SkipWs(json, ref i);
                if (i >= json.Length) break;
                if (json[i] == ']') break;

                if (json[i] == '"')
                {
                    i++;
                    var sb = new StringBuilder();
                    while (i < json.Length && json[i] != '"')
                    {
                        if (json[i] == '\\' && i + 1 < json.Length)
                        {
                            char next = json[i + 1];
                            if (next == 'n') sb.Append('\n');
                            else if (next == 't') sb.Append('\t');
                            else if (next == 'r') sb.Append('\r');
                            else sb.Append(next);
                            i += 2;
                        }
                        else
                        {
                            sb.Append(json[i]);
                            i++;
                        }
                    }
                    if (i < json.Length) i++;
                    result.Add(sb.ToString());
                }
                else
                {
                    i++;
                }

                SkipWs(json, ref i);
                if (i < json.Length && json[i] == ',') i++;
            }

            return result;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r')) i++;
        }
    }
}
