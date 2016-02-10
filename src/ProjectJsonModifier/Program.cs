using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ProjectJsonModifier
{
    public class Program
    {
        const string gitReposRootDir = @"C:\dotnetxunitrepos";
        const string gitRepoWorkDirFormat = gitReposRootDir + @"\{0}";
        const string gitReposUrlFormat = "https://github.com/aspnet/{0}.git";

        public static void Main(string[] args)
        {
            var appSettings = JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText("appSettings.json"));

            if (!Directory.Exists(gitReposRootDir))
            {
                Directory.CreateDirectory(gitReposRootDir);
            }

            foreach (var repoName in appSettings.Repositories)
            {
                    Console.WriteLine("Cloning repository '{0}'...", repoName);
                var repoRootDir = string.Format(gitRepoWorkDirFormat, repoName);
                var repoLocation = Repository.Clone(string.Format(gitReposUrlFormat, repoName), repoRootDir);
                var gitRepo = new Repository(repoLocation);
                var branch = gitRepo.CreateBranch("kiran/dotnet-xunit-changes");
                gitRepo.Checkout(branch);

                // find test directories in the repo
                var testDirRoot = Path.Combine(repoRootDir, "test");
                foreach (var testProject in Directory.GetDirectories(testDirRoot))
                {
                    var testProjectJson = Path.Combine(testDirRoot, testProject, "project.json");
                    if (!File.Exists(testProjectJson))
                    {
                        break;
                    }
                    var projectJson = JObject.Parse(File.ReadAllText(testProjectJson));


                    var commands = projectJson["commands"];
                    if (commands != null)
                    {
                        projectJson.Remove("commands");
                    }

                    var frameworks = projectJson["frameworks"];
                    if (frameworks != null)
                    {
                        var dnxcore50 = frameworks["dnxcore50"];
                        if (dnxcore50 != null)
                        {
                            var dependencies = dnxcore50["dependencies"];
                            if (dependencies != null)
                            {
                                var oldXunitRunner = dependencies["xunit.runner.aspnet"];
                                if (oldXunitRunner != null)
                                {
                                    oldXunitRunner.Parent.Remove();
                                }
                            }
                            else
                            {
                                dnxcore50["dependencies"] = new JObject();
                            }

                            dnxcore50["imports"] = "portable-net451+win8";
                            dnxcore50["dependencies"]["dotnet-test-xunit"] = "1.0.0-dev-*";
                            dnxcore50["dependencies"]["Microsoft.NETCore.Platforms"] = "1.0.1-*";

                            var newFrameworks = new JObject();
                            foreach (var property in frameworks.Children())
                            {
                                if (((JProperty)property).Name.Equals("dnxcore50", StringComparison.OrdinalIgnoreCase))
                                {
                                    newFrameworks.AddFirst(property);
                                    continue;
                                }

                                newFrameworks.Add(property);
                            }
                            projectJson["frameworks"] = newFrameworks;
                        }
                    }

                    using (FileStream fs = File.Open(Path.Combine(testProject, "project.json"), FileMode.OpenOrCreate | FileMode.Truncate, FileAccess.ReadWrite))
                    {
                        using (StreamWriter sw = new StreamWriter(fs))
                        {
                            using (JsonTextWriter jw = new JsonTextWriter(sw))
                            {
                                var serializer = JsonSerializer.Create(new JsonSerializerSettings()
                                {
                                    Formatting = Formatting.Indented
                                });

                                serializer.Serialize(jw, projectJson);
                            }
                        }
                    }

                    //if (gitRepo.RetrieveStatus().IsDirty)
                    //{
                    //    gitRepo.Commit("Enable tests to use dotnet xunit test runner");
                    //}
                }

            }
        }
    }
}
