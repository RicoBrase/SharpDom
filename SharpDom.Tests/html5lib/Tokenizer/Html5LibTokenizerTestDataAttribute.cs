using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using SharpDom.Tests.html5lib.Tokenizer.OutputTokens;
using Xunit.Sdk;

namespace SharpDom.Tests.html5lib.tokenizer
{
    public class Html5LibTokenizerTestDataAttribute : DataAttribute
    {
        private readonly string _testsJson;
        
        public Html5LibTokenizerTestDataAttribute(string testsFilePath)
        {
            var path = Path.GetRelativePath(Directory.GetCurrentDirectory(), testsFilePath);
            if (!File.Exists(path)) throw new ArgumentException($"Could not find test file at path: {path}");

            _testsJson = File.ReadAllText(testsFilePath);
        }
        
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            if (testMethod == null) throw new ArgumentNullException(nameof(testMethod));
            if(_testsJson == null) throw new NullReferenceException(_testsJson);
            
            var suiteJson = JObject.Parse(_testsJson);
            
            var tests = new List<Html5LibTokenizerTestData>();

            if (!suiteJson.ContainsKey("tests")) throw new ArgumentException("Required key 'tests' not found.");

            var testsJson = (JArray) suiteJson["tests"];
            var index = 0;
            foreach (var t in testsJson!)
            {
                var testJson = (JObject) t;
                var outputJson = (JArray) testJson["output"];
                var outputTokens = new List<Html5LibTokenizerTestOutputToken>();
                var initialStates = new List<string>();

                foreach (var jToken in outputJson!)
                {
                    var output = (JArray) jToken;
                    if (output.Count == 0) throw new Exception("Empty test output entry.");
                    var tokenName = (string) output[0];
                    switch (tokenName)
                    {
                        case "DOCTYPE":
                            if (output.Count != 5)
                                throw new Exception("Malformed output token data. Expected 5 data entries.");
                            outputTokens.Add(new Html5LibTokenizerTestOutputDoctypeToken
                            {
                                Name = (string)output[1],
                                PublicId = (string)output[2],
                                SystemId = (string)output[3],
                                Correctness= (bool)output[4],
                            });
                            break;
                        case "StartTag":
                            if(output.Count is < 3 or > 4) throw new Exception("Malformed output token data. Expected 3-4 data entries.");

                            var testOutputData = new Html5LibTokenizerTestOutputStartTagToken
                            {
                                Name = (string) output[1],
                                SelfClosing = output.Count == 4 && (bool) output[3]
                            };
                            AddAttributesToTestOutputData(testOutputData.Attributes, (JObject)output[2]);
                            outputTokens.Add(testOutputData);
                            break;
                        case "EndTag":
                            if(output.Count != 2) throw new Exception("Malformed output token data. Expected 2 data entries.");
                            outputTokens.Add(new Html5LibTokenizerTestOutputEndTagToken
                            {
                                Name = (string)output[1]
                            });
                            break;
                        case "Comment":
                            if(output.Count != 2) throw new Exception("Malformed output token data. Expected 2 data entries.");
                            outputTokens.Add(new Html5LibTokenizerTestOutputCommentToken
                            {
                                Data = (string)output[1]
                            });
                            break;
                        case "Character":
                            if(output.Count != 2) throw new Exception("Malformed output token data. Expected 2 data entries.");
                            outputTokens.Add(new Html5LibTokenizerTestOutputCharacterToken
                            {
                                Data = (string)output[1]
                            });
                            break;
                        default:
                            throw new Exception("Unknown output token type.");
                    }
                }

                if (testJson.ContainsKey("initialStates"))
                {
                    var initialStatsJson = (JArray) testJson["initialStates"];
                    foreach (string state in initialStatsJson!)
                    {
                        initialStates.Add(state);
                    }
                }
                
                
                tests.Add(new Html5LibTokenizerTestData
                {
                    Index = $"{index++}",
                    Description = (string)testJson["description"],
                    InitialStates = initialStates.ToArray(),
                    Input = (string)testJson["input"],
                    Output = outputTokens.ToArray()
                });
            }

            var maxIndexString = index.ToString();
            
            // TODO: Run ALL tests, not only those without "initialStates"
            var testData = tests.Where(it => it.InitialStates.Length == 0)
                .Select(it =>
                {
                    while (maxIndexString.Length > it.Index.Length)
                    {
                        it.Index = $"0{it.Index}";
                    }
                    return it;
                })
                .OrderBy(it => it.Index)
                .Select(test => new object[] {test})
                .ToList();
            return testData;
        }

        private static void AddAttributesToTestOutputData(IDictionary<string, string> dataAttributeList, JObject jsonAttributes)
        {
            foreach (var (key, value) in jsonAttributes)
            {
                dataAttributeList.Add(key, (string) value);
            }
        }
    }
}