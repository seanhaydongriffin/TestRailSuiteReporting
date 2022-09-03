using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedProject;
using SharedProject.Models;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using Toolkit.Windows;

namespace TestRailSuiteReporting
{
    class Program
    {
        private static SharedProject.TestRail.APIClient TestRailClient = null;

        static void Main(string[] args)
        {
            Log.Initialise(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\TestRailSuiteReporting.log");
            Log.Initialise(null);
            AppConfig.Open();
            
            TestRailClient = new SharedProject.TestRail.APIClient(AppConfig.Get("TestRailUrl"));
            TestRailClient.User = AppConfig.Get("TestRailUser");
            TestRailClient.Password = AppConfig.Get("TestRailPassword");

            var today = System.DateTime.Today;
            var unixTimestamp = (int)today.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;
            Log.WriteLine("today = " + today);
            Log.WriteLine("unixTimestamp = " + unixTimestamp);

            // test statuses

            //var test_statuses = (JArray)TestRailClient.SendGet("get_statuses");

            SharedProject.Confluence.APIClient ConfluenceClient = new SharedProject.Confluence.APIClient(AppConfig.Get("ConfluenceUrl"));
            ConfluenceClient.User = AppConfig.Get("ConfluenceUser");
            ConfluenceClient.Password = AppConfig.Get("ConfluenceApiToken");

            // var debug = (JObject)ConfluenceClient.SendGet(AppConfig.Get("ConfluencePageKey") + "?expand=body.storage");
            //var debug = (JObject)ConfluenceClient.SendGet("2188705921?expand=body.storage");
            //var debug = (JObject)ConfluenceClient.SendGet("997687643/child/page?limit=200");
            //Log.WriteLine(debug.ToString());

            var confluence_page_storage_str = "<ac:structured-macro ac:name=\"toc\" ac:schema-version=\"1\" data-layout=\"default\"><ac:parameter ac:name=\"minLevel\">1</ac:parameter><ac:parameter ac:name=\"maxLevel\">2</ac:parameter></ac:structured-macro><hr />";
            
            var testrail_plan_type_status_count = new Dictionary<string, int?>();

            Log.WriteLine("Getting case fields ...");
            var case_fields = (JArray)TestRailClient.SendGet("get_case_fields");

            var tags = case_fields.SelectToken("$[?(@.name == 'tags')]");
            var tags_options = tags["configs"][0]["options"]["items"].ToString();
            var tag = tags_options.Split('\n').Select(s => s.Split(',')).ToDictionary(a => Int32.Parse(a[0]), a => a[1].Trim());

            var customers = case_fields.SelectToken("$[?(@.name == 'customer')]");
            var customers_options = customers["configs"][0]["options"]["items"].ToString();
            var customer = customers_options.Split('\n').Select(s => s.Split(',')).ToDictionary(a => Int32.Parse(a[0]), a => a[1].Trim());

            // The projects

            var project_num = 0;

            foreach (XmlNode TestProject in AppConfig.GetSectionGroup("TestProjects").GetSectionGroups())
            {
                project_num++;
                var TestProjectId = TestProject.FirstChild.GetAttributeValue("Id");
                var TestProjectName = TestProject.FirstChild.GetAttributeValue("Name");
                confluence_page_storage_str += "<h1><u><a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/projects/overview/" + TestProjectId + "\">" + TestProjectName + "</a></u></h1>";

                // The test suites

                var suite_num = 0;

                foreach (XmlNode TestSuite in TestProject.SelectNodes("TestSuites/add"))
                {
                    suite_num++;
                    var TestSuiteId = TestSuite.GetAttributeValue("Id");

                    Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " \"" + TestProjectName + "\", Suite " + suite_num + " of " + TestProject.SelectNodes("TestSuites/add").Count + " getting details ...");
                    var suite = (JObject)TestRailClient.SendGet("get_suite/" + TestSuiteId);

                    confluence_page_storage_str += "<h2><u><a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/projects/overview/" + TestProjectId + "\">" + suite["name"] + "</a></u></h2>";
                    confluence_page_storage_str += "<p>" + suite["description"] + "</p>";
                    confluence_page_storage_str += "<table data-layout=\"full-width\"><colgroup><col style=\"width:500px;\"/><col style=\"width:150px;\"/><col style=\"width:150px;\"/><col style=\"width:150px;\"/></colgroup><tbody><tr><td><sub><b>[ID] Title</b></sub></td><td><sub><b>Tags</b></sub></td><td><sub><b>Customer</b></sub></td><td><sub><b>Script Name</b></sub></td></tr>";

                    // the test cases

                    var cases_table = new DataTable();
                    cases_table.Columns.Add("ID", typeof(int));
                    cases_table.Columns.Add("Title", typeof(string));
                    cases_table.Columns.Add("Tags", typeof(string));
                    cases_table.Columns.Add("Customers", typeof(string));
                    cases_table.Columns.Add("ScriptName", typeof(string));

                    Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " \"" + TestProjectName + "\", Suite " + suite_num + " of " + TestProject.SelectNodes("TestSuites/add").Count + " getting cases ...");
                    var cases = (JObject)TestRailClient.SendGet("get_cases/" + TestProjectId + "&suite_id=" + TestSuiteId);

                    foreach (var testcase in cases["cases"])
                    {
                        var custom_tag_str = "";

                        foreach (var custom_tag in testcase["custom_tags"])
                        {
                            if (custom_tag_str.Length > 0)

                                custom_tag_str = custom_tag_str + ", ";

                            custom_tag_str = custom_tag_str + tag[custom_tag.ToObject<int>()];
                        }

                        var custom_customer_str = "";

                        foreach (var custom_customer in testcase["custom_customer"])
                        {
                            if (custom_customer_str.Length > 0)

                                custom_customer_str = custom_customer_str + ", ";

                            custom_customer_str = custom_customer_str + customer[custom_customer.ToObject<int>()];
                        }

                        cases_table.Rows.Add(testcase["id"], testcase["title"], custom_tag_str, custom_customer_str, testcase["custom_auto_script_ref"]);
//                        confluence_page_storage_str += "<tr><td><sub>[" + testcase["id"] + "] " + testcase["title"] + "</sub><ac:structured-macro ac:name=\"anchor\"><ac:parameter ac:name=\"\">C" + testcase["id"] + "</ac:parameter></ac:structured-macro></td><td><sub>" + custom_tag_str + "</sub></td><td><sub>" + custom_customer_str + "</sub></td><td><sub>" + testcase["custom_auto_script_ref"] + "</sub></td></tr>";
                    }

                    cases_table.DefaultView.Sort = "Title ASC";

                    foreach (DataRow row in cases_table.DefaultView.ToTable().Rows)
                    {
                        // ... Write value of first field as integer.
//                        Console.WriteLine(row.Field<int>(0));
  //                      Console.WriteLine(row.Field<string>(1));

                        confluence_page_storage_str += "<tr><td><sub>[" + row.Field<int>(0) + "] " + row.Field<string>(1) + "</sub><ac:structured-macro ac:name=\"anchor\"><ac:parameter ac:name=\"\">C" + row.Field<int>(0) + "</ac:parameter></ac:structured-macro></td><td><sub>" + row.Field<string>(2) + "</sub></td><td><sub>" + row.Field<string>(3) + "</sub></td><td><sub>" + row.Field<string>(4) + "</sub></td></tr>";

                    }


                    confluence_page_storage_str += "</tbody></table>";
                }
            }


            // Update the confluence page

            Log.WriteLine("Confluence Page \"" + AppConfig.Get("ConfluencePage") + "\" getting the version ...");
            var confluence_page = (JObject)ConfluenceClient.SendGet(AppConfig.Get("ConfluencePage") + "?expand=version");
            var confluence_page_version = (long)confluence_page["version"]["number"];
            confluence_page_version++;

            var confluence_json = new
            {
                @version = new
                {
                    number = confluence_page_version
                },
                type = "page",
                title = "QA Test Suites",
                //@metadata = new
                //{
                //    @properties = new
                //    {
                //        @contentappearancedraft = new
                //        {
                //            value = "full-width"
                //        },
                //        @contentappearancepublished = new
                //        {
                //            value = "full-width"
                //        }
                //    }
                //},
                @space = new
                {
                    key = AppConfig.Get("ConfluenceSpace")
                },
                @ancestors = new[] {
                    new {
                        id = AppConfig.Get("ConfluenceAncestor")
                    }
                }.ToList(),
                @body = new
                {
                    @storage = new
                    {
                        value = confluence_page_storage_str,
                        representation = "storage"
                    }
                }
            };

            Log.WriteLine("Confluence Page \"" + AppConfig.Get("ConfluencePage") + "\" updating ...");
            var pp = (JToken)ConfluenceClient.SendPut(AppConfig.Get("ConfluencePage").ToString(), confluence_json);

/*

            var TestProjectName2 = TestProject.GetAttributeValue("Name");
                var TestProjectId2 = TestProject.GetAttributeValue("Id");
                var TestProjectConfluenceRootKey = TestProject.GetAttributeValue("ConfluenceSpace") + "-" + TestProject.GetAttributeValue("ConfluencePage") + "-" + TestProject.GetAttributeValue("Team");

                if (confluence_page_storage[TestProjectConfluenceRootKey] == null)
                {
                    //confluence_page_storage.Add(TestProjectConfluenceRootKey, "<ac:structured-macro ac:name=\"info\"><ac:rich-text-body>Current Sprint is <strong>" + current_sprint_with_date + "</strong></ac:rich-text-body></ac:structured-macro>");
                    confluence_page_storage.Add(TestProjectConfluenceRootKey, "<ac:structured-macro ac:name=\"toc\" ac:schema-version=\"1\" data-layout=\"default\"><ac:parameter ac:name=\"minLevel\">1</ac:parameter><ac:parameter ac:name=\"maxLevel\">2</ac:parameter></ac:structured-macro>");
                    confluence_page_storage.Add(TestProjectConfluenceRootKey, "<hr />");
                }

                confluence_page_storage.Add(TestProjectConfluenceRootKey, "<h1><u><a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/projects/overview/" + TestProjectId + "\">" + TestProjectName + "</a></u></h1>");

                // The quarter milestone

                Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " \"" + TestProjectName + "\" getting the milestones ...");
                var milestones = (JObject)TestRailClient.SendGet("get_milestones/" + TestProjectId);
                var quarter_milestone = milestones.SelectToken("$..[?(@.name =~ /^FY" + current_quarter.ShortYear + "Q" + current_quarter.Quarter + " .*$/)]");

                if (quarter_milestone == null)

                    Environment.Exit(0);

                // the sprint milestone

                var sprint_milestones = quarter_milestone["milestones"];
                var sprint_milestone = sprint_milestones.SelectToken("$..[?(@.start_on <= " + unixTimestamp + " && @.due_on >= " + unixTimestamp + ")]");
                        
                if (sprint_milestone == null)

                    Environment.Exit(0);

                current_sprint = sprint_milestone["name"].ToString().Split(' ').FirstOrDefault();

                // The plans

                Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " Milestone \"" + sprint_milestone["name"] + "\" getting the plans ...");

                var plans = (JObject)TestRailClient.SendGet("get_plans/" + TestProjectId + "&milestone_id=" + sprint_milestone["id"]);

                foreach (var plan in plans["plans"])
                {
                    confluence_page_storage.Add(TestProjectConfluenceRootKey, "<h2><u><a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/plans/view/" + plan["id"] + "\">" + plan["name"].ToString().Replace(current_sprint, "").Trim() + "</a></u></h2>");

                    // The runs

                    Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " Plan \"" + plan["name"] + "\" getting the details ...");

                    var plan_detail = (JObject)TestRailClient.SendGet("get_plan/" + plan["id"]);

                    foreach (var entry in plan_detail["entries"])
                    {
                        foreach (var run in entry["runs"])
                        {
                            confluence_page_storage.Add(TestProjectConfluenceRootKey, "<h3><a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/runs/view/" + run["id"] + "\">" + run["name"] + " (" + run["config"] + ")</a></h3>");
                            confluence_page_storage.Add(TestProjectConfluenceRootKey, "<table data-layout=\"full-width\"><colgroup><col style=\"width:110px;\"/><col style=\"width:280px;\"/><col style=\"width:50px;\"/><col style=\"width:60px;\"/><col style=\"width:100px;\"/></colgroup><tbody><tr><td><sub><b>Name (ID)</b></sub></td><td><sub><b>Title</b></sub></td><td><sub><b>Status</b></sub></td><td><sub><b>Tested On</b></sub></td><td><sub><b>All Defects</b></sub></td></tr>");

                            // The tests & results

                            Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " Run \"" + run["name"] + " (" + run["config"] + ")\" getting the tests ...");

                            var tests = (JObject)TestRailClient.SendGet("get_tests/" + run["id"]);

                            Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " Run \"" + run["name"] + " (" + run["config"] + ")\" getting the results ...");

                            var results = (JObject)TestRailClient.SendGet("get_results_for_run/" + run["id"]);
                            var tested_on = "";
                            var all_defects = "";

                            foreach (var test in tests["tests"])
                            {
                                try
                                {
                                    var latest_test_result = results.SelectTokens("$.results[?(@.test_id == " + (long)test["id"] + ")]").First();
                                    var created_on = SharedProject.DateTime.UnixTimeStampToDateTime((double)latest_test_result["created_on"]);
                                    tested_on = created_on.ToString("dd MMM h:mm tt");
                                    all_defects = latest_test_result["defects"].ToString();
                                }
                                catch (Exception e)
                                {
                                }

                                var status_emoticon = "";

                                if (AppConfig.Get("TestRailTestStatus" + test["status_id"]).Equals("Passed"))

                                    status_emoticon = "<ac:emoticon ac:name=\"tick\" /> ";

                                if (AppConfig.Get("TestRailTestStatus" + test["status_id"]).Equals("Failed"))

                                    status_emoticon = "<ac:emoticon ac:name=\"cross\" /> ";

                                if (AppConfig.Get("TestRailTestStatus" + test["status_id"]).Equals("Untested"))

                                    status_emoticon = "<ac:emoticon ac:name=\"flag_off\" ac:emoji-shortname=\":flag_off:\" ac:emoji-id=\"atlassian-flag_off\" ac:emoji-fallback=\":flag_off:\" /> ";

                                confluence_page_storage.Add(TestProjectConfluenceRootKey, "<tr><td><sub>" + test["custom_auto_script_ref"] + " (<a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/cases/view/" + test["case_id"] + "\">C" + test["case_id"] + "</a>)</sub><ac:structured-macro ac:name=\"anchor\"><ac:parameter ac:name=\"\">C" + test["case_id"] + "</ac:parameter></ac:structured-macro></td><td><sub>" + Regex.Replace(Regex.Replace(test["title"].ToString().UrlEncode(5), "VARIANT on (\\w+)", "VARIANT on <a href=\"#$1\">$1</a>"), "DEPENDANT on (\\w+)", "DEPENDANT on <a href=\"#$1\">$1</a>") + "</sub></td><td><sub>" + status_emoticon + "<a href=\"" + AppConfig.Get("TestRailUrl") + "/index.php?/tests/view/" + test["id"] + "\">" + AppConfig.Get("TestRailTestStatus" + test["status_id"]) + "</a></sub></td><td><sub>" + tested_on + "</sub></td><td><sub>" + all_defects + "</sub></td></tr>");

                                // Tally the test results for later pie charting

                                testrail_plan_type_status_count.Increment(plan["name"].ToString().Replace(current_sprint, "").Trim() + "-" + AppConfig.Get("TestRailTestStatus" + test["status_id"]));
                            }

                            confluence_page_storage.Add(TestProjectConfluenceRootKey, "</tbody></table>");
                        }
                    }
                }
            }

            // for each confluence page to update

            foreach (string confluence_root_key in confluence_page_storage)
            {
                var confluence_space_key = confluence_root_key.Split('-')[0];
                var confluence_parent_page_key = confluence_root_key.Split('-')[1];
                var team_name = confluence_root_key.Split('-')[2];

                // Check if the sprint page exists in Confluence (under the provided root page provided in the config)

                var confluence_child_page = (JObject)ConfluenceClient.SendGet(confluence_parent_page_key + "/child/page?limit=200");

                var sprint_page_key = confluence_child_page.SelectToken("$..results[?(@.title == '" + team_name + " " + current_sprint + "')].id");

                if (sprint_page_key == null)
                {
                    // Create the Sprint Page

                    Log.WriteLine("Confluence Page \"" + team_name + " " + current_sprint + "\" creating ...");

                    var confluence_create_page_json = new
                    {
                        type = "page",
                        title = team_name + " " + current_sprint,
                        @space = new
                        {
                            key = confluence_space_key
                        },
                        @ancestors = new[] {
                            new {
                                id = confluence_parent_page_key
                            }
                        }.ToList()
                    };

                    var result = (JObject)ConfluenceClient.SendPost("", confluence_create_page_json);
                    sprint_page_key = result["id"];
                }

                confluence_child_page = (JObject)ConfluenceClient.SendGet(sprint_page_key + "/child/page?limit=200");

                var sprint_qa_page_key = confluence_child_page.SelectToken("$..results[?(@.title == '" + team_name + " " + current_sprint + " QA')].id");

                if (sprint_qa_page_key == null)
                {
                    // Create the Sprint QA Page

                    Log.WriteLine("Confluence Page \"" + team_name + " " + current_sprint + " QA\" creating ...");

                    var confluence_create_page_json = new
                    {
                        type = "page",
                        title = team_name + " " + current_sprint + " QA",
                        @space = new
                        {
                            key = confluence_space_key
                        },
                        @ancestors = new[] {
                            new {
                                id = sprint_page_key
                            }
                        }.ToList()
                    };

                    var result = (JObject)ConfluenceClient.SendPost("", confluence_create_page_json);
                    sprint_qa_page_key = result["id"];
                }

                //var confluence_page_storage_str = string.Join("", confluence_page_storage.GetValues(confluence_root_key));

                // Prepend pie charts (to top of) confluence page

                var section_width = "default";      // back to center
                //var section_width = "wide";      // go wide
                //var section_width = "full-width"; // go full wide
                var chart_size = 200;
                var pie_charts_storage_str = "<ac:structured-macro ac:name=\"section\" ac:align=\"center\" data-layout=\"" + section_width + "\"><ac:parameter ac:name=\"border\">true</ac:parameter><ac:rich-text-body>";
                var testrail_plan_type_status_processed = new Dictionary<string, bool?>();

                foreach (var entry in testrail_plan_type_status_count)
                {
                    var key = entry.Key.Substring(0, entry.Key.LastIndexOf('-'));

                    if (testrail_plan_type_status_processed.get(key) == null)
                    {
                        testrail_plan_type_status_processed.put(key, true);

                        var pie_chart = "<ac:structured-macro ac:name=\"chart\"><ac:parameter ac:name=\"subTitle\">" + key + "</ac:parameter><ac:parameter ac:name=\"type\">pie</ac:parameter><ac:parameter ac:name=\"width\">" + chart_size + "</ac:parameter><ac:parameter ac:name=\"height\">" + chart_size + "</ac:parameter><ac:parameter ac:name=\"legend\">false</ac:parameter><ac:parameter ac:name=\"pieSectionLabel\">%1%</ac:parameter><ac:parameter ac:name=\"colors\">green,red,gray</ac:parameter><ac:rich-text-body><table><tbody>" +
                                        "<tr><th><p>Total</p></th><th><p>Passed</p></th><th><p>Failed</p></th><th><p>Untested</p></th></tr>" +
                                        "<tr><th><p>Total</p></th><td><p>" + testrail_plan_type_status_count.get(key + "-Passed", 0) + "</p></td><td><p>" + testrail_plan_type_status_count.get(key + "-Failed", 0) + "</p></td><td><p>" + testrail_plan_type_status_count.get(key + "-Untested", 0) + "</p></td></tr>" +
                                        "</tbody></table></ac:rich-text-body></ac:structured-macro>";

                        pie_charts_storage_str = pie_charts_storage_str + "<ac:structured-macro ac:name=\"column\" ac:align=\"center\"><ac:rich-text-body>" + pie_chart + "</ac:rich-text-body></ac:structured-macro>";
                    }
                }

                pie_charts_storage_str = pie_charts_storage_str + "</ac:rich-text-body></ac:structured-macro>";
                confluence_page_storage_str = pie_charts_storage_str + confluence_page_storage_str;

                // Update the confluence page

                Log.WriteLine("Confluence Page \"" + sprint_qa_page_key + "\" getting the version ...");
                var confluence_page = (JObject)ConfluenceClient.SendGet(sprint_qa_page_key + "?expand=version");
                var confluence_page_version = (long)confluence_page["version"]["number"];
                confluence_page_version++;

                var confluence_json = new
                {
                    @version = new
                    {
                        number = confluence_page_version
                    },
                    type = "page",
                    title = team_name + " " + current_sprint + " QA",
                    @space = new
                    {
                        key = confluence_space_key
                    },
                    @ancestors = new[] {
                        new {
                            id = sprint_page_key
                        }
                    }.ToList(),
                    @body = new
                    {
                        @storage = new
                        {
                            value = confluence_page_storage_str,
                            representation = "storage"
                        }
                    }
                };

                Log.WriteLine("Confluence Page \"" + sprint_qa_page_key + "\" updating ...");
                var pp = (JToken)ConfluenceClient.SendPut(sprint_qa_page_key.ToString(), confluence_json);

            }*/
        } 
    }
}
