using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Island.Core.StreamBox
{
    /// <summary>
    /// Reading helpers for StreamBox responses, with one opinion: be strict about what we need and
    /// relaxed about everything else.
    /// </summary>
    /// <remarks>
    /// <para>
    /// StreamBox is someone else's project, under active development, and it will add fields and
    /// enum cases without telling us. If Islandia threw on anything it did not recognise, a build
    /// in a headset would start refusing to show a library because the server learned a new word.
    /// So: unknown fields are ignored, and an unknown value for a known enum falls back to a
    /// documented case the caller can handle.
    /// </para>
    /// <para>
    /// The other half of that bargain is that a field we genuinely need, and that the contract
    /// says is always present, is an error when it is missing — a named one, with its path.
    /// Nothing silently becomes zero or empty string.
    /// </para>
    /// </remarks>
    public static class Json
    {
        public static JObject ParseObject(string text, string what = "response")
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new StreamBoxContractException(what, "the body was empty");
            try
            {
                var token = JToken.Parse(text);
                if (token is JObject obj) return obj;
                throw new StreamBoxContractException(what, $"expected an object, found {Describe(token)}");
            }
            catch (Newtonsoft.Json.JsonReaderException error)
            {
                throw new StreamBoxContractException(what, "the body was not valid JSON: " + error.Message);
            }
        }

        // ---- required ---------------------------------------------------------------------------

        public static string RequiredString(JObject owner, string field, string path)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null)
                throw new StreamBoxContractException(Join(path, field), "it is missing");
            var text = value.Type == JTokenType.String ? value.Value<string>() : value.ToString();
            if (string.IsNullOrEmpty(text))
                throw new StreamBoxContractException(Join(path, field), "it is empty");
            return text;
        }

        public static int RequiredInt(JObject owner, string field, string path)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null)
                throw new StreamBoxContractException(Join(path, field), "it is missing");
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
                throw new StreamBoxContractException(Join(path, field), $"expected a number, found {Describe(value)}");
            return value.Value<int>();
        }

        public static long RequiredLong(JObject owner, string field, string path)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null)
                throw new StreamBoxContractException(Join(path, field), "it is missing");
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float)
                throw new StreamBoxContractException(Join(path, field), $"expected a number, found {Describe(value)}");
            return value.Value<long>();
        }

        public static bool RequiredBool(JObject owner, string field, string path)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null)
                throw new StreamBoxContractException(Join(path, field), "it is missing");
            if (value.Type != JTokenType.Boolean)
                throw new StreamBoxContractException(Join(path, field), $"expected true or false, found {Describe(value)}");
            return value.Value<bool>();
        }

        public static JObject RequiredObject(JObject owner, string field, string path)
        {
            if (!(owner?[field] is JObject child))
                throw new StreamBoxContractException(Join(path, field), "expected an object");
            return child;
        }

        /// <summary>
        /// A required array, mapped element by element. The path of a failing element names its
        /// index, so a bad item in a page of eighty is findable.
        /// </summary>
        public static List<T> RequiredArray<T>(
            JObject owner, string field, string path, Func<JObject, string, T> mapElement)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null)
                throw new StreamBoxContractException(Join(path, field), "it is missing");
            if (!(value is JArray array))
                throw new StreamBoxContractException(Join(path, field), $"expected an array, found {Describe(value)}");
            return MapArray(array, Join(path, field), mapElement);
        }

        // ---- optional ----------------------------------------------------------------------------

        public static string OptionalString(JObject owner, string field)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null) return null;
            var text = value.Type == JTokenType.String ? value.Value<string>() : value.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        public static int? OptionalInt(JObject owner, string field)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) return null;
            return value.Value<int>();
        }

        public static long? OptionalLong(JObject owner, string field)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) return null;
            return value.Value<long>();
        }

        public static double? OptionalDouble(JObject owner, string field)
        {
            var value = owner?[field];
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.Integer && value.Type != JTokenType.Float) return null;
            return value.Value<double>();
        }

        /// <summary>An absent boolean is <paramref name="fallback"/>, never an exception.</summary>
        public static bool OptionalBool(JObject owner, string field, bool fallback = false)
        {
            var value = owner?[field];
            if (value == null || value.Type != JTokenType.Boolean) return fallback;
            return value.Value<bool>();
        }

        public static JObject OptionalObject(JObject owner, string field) => owner?[field] as JObject;

        /// <summary>An absent or malformed array is empty, not an error.</summary>
        public static List<T> OptionalArray<T>(
            JObject owner, string field, string path, Func<JObject, string, T> mapElement)
        {
            if (!(owner?[field] is JArray array)) return new List<T>();
            return MapArray(array, Join(path, field), mapElement);
        }

        /// <summary>An absent or malformed array of strings is empty, not an error.</summary>
        public static List<string> OptionalStrings(JObject owner, string field)
        {
            var result = new List<string>();
            if (!(owner?[field] is JArray array)) return result;
            foreach (var element in array)
            {
                if (element.Type == JTokenType.String) result.Add(element.Value<string>());
            }
            return result;
        }

        // ---- enums -------------------------------------------------------------------------------

        /// <summary>
        /// Maps a string onto a known case, falling back rather than throwing.
        /// </summary>
        /// <remarks>
        /// This is the single most important tolerance in the client. When StreamBox adds, say, a
        /// new item kind, every existing Islandia build should carry on and show that item as
        /// something it does not recognise — not fail to parse the page it arrived in.
        /// </remarks>
        public static T Enum<T>(JObject owner, string field, IReadOnlyDictionary<string, T> known, T fallback)
        {
            var text = OptionalString(owner, field);
            if (text == null) return fallback;
            return known.TryGetValue(text, out var value) ? value : fallback;
        }

        // ---- shared ------------------------------------------------------------------------------

        private static List<T> MapArray<T>(JArray array, string path, Func<JObject, string, T> mapElement)
        {
            var result = new List<T>(array.Count);
            for (var index = 0; index < array.Count; index++)
            {
                if (!(array[index] is JObject element))
                    throw new StreamBoxContractException($"{path}[{index}]", $"expected an object, found {Describe(array[index])}");
                result.Add(mapElement(element, $"{path}[{index}]"));
            }
            return result;
        }

        private static string Join(string path, string field) =>
            string.IsNullOrEmpty(path) ? field : path + "." + field;

        private static string Describe(JToken token) =>
            token == null ? "nothing" : token.Type.ToString().ToLowerInvariant();
    }
}
