using System;
using System.IO;
using System.Linq;
using System.Net;
using Newtonsoft.Json.Linq;

namespace SharpDom.Infra.Unicode
{
    public class NamedCharacterReference
    {
        private const string PathToEntitiesJson = "Resources/entities.json";

        private static NamedCharacterReference _instance = null;

        private static NamedCharacterReference Instance
        {
            get { return _instance ??= new NamedCharacterReference(); }
        }

        private JObject _entities;

        private NamedCharacterReference()
        {
            var path = Path.GetRelativePath(Directory.GetCurrentDirectory(), PathToEntitiesJson);
            if (!File.Exists(path)) throw new ArgumentException($"Could not find entities file at path: {path}");

            _entities = JObject.Parse(File.ReadAllText(PathToEntitiesJson));
        }

        public static bool IsNamedCharacterReference(string input)
        {
            return Instance._entities.ContainsKey(input);
        }

        public static char[] GetCodepointsOfNamedCharacterReference(string input)
        {
            if (!Instance._entities.TryGetValue(input, out var codepointsJson))
            {
                throw new ArgumentException($"Unknown named character reference: {input}", nameof(input));
            }

            var codepoints = (JArray)codepointsJson["codepoints"];
            return codepoints.Select(it => (char) it).ToArray();
        }
    }
}