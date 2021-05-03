using System;
using System.Collections.Generic;
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

        private readonly JObject _entities;

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
            if (codepoints is null) throw new Exception(@"Missing ""codepoints"" key in entities.json file");
            return codepoints.Select(it => (char) it).ToArray();
        }

        public static int GetAllPossibleNamedCharacterReferences(string input)
        {
            return Instance._entities.Properties().Count(it => it.Name.StartsWith(input));
        }
    }
}