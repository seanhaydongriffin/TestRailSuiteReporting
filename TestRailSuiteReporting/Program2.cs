using Markdig.Syntax.Inlines;
using Newtonsoft.Json.Linq;
using SharedProject;
using SharedProject.Confluence;
using SharedProject.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace TestRailSuiteReporting
{
    class Program2
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

            //var case_types = (JArray)TestRailClient.SendGet("get_case_types");
            var case_type_dict = new Dictionary<int, string>();

            for (int case_type_num = 1; case_type_num < 50; case_type_num++)
            {
                if (AppConfig.Get("TestRailCaseType" + case_type_num) != null)

                    case_type_dict.Add(case_type_num, AppConfig.Get("TestRailCaseType" + case_type_num));
            }

            // test statuses

            //var test_statuses = (JArray)TestRailClient.SendGet("get_statuses");

            Log.WriteLine("Getting milestones for TestRail Janison Assessment project ...");
            var milestones = (JObject)TestRailClient.SendGet("get_milestones/" + 64);

            // Current quarter

            // Look for a quarter milestone that is active

            var quarter_milestone = milestones.SelectToken("$..[?(@.parent_id == null && @.started_on <= " + (System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds + 3600) + " && @.due_on >= " + System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds + ")]");

            if (quarter_milestone == null)

                // Look for a quarter milestone that is not active

                quarter_milestone = milestones.SelectToken("$..[?(@.parent_id == null && @.start_on <= " + unixTimestamp + " && @.due_on >= " + unixTimestamp + ")]");

            // Current sprint

            // Look for a sprint milestone that is active

            var sprint_milestones = quarter_milestone["milestones"];

            var sprint_milestone = sprint_milestones.SelectToken("$..[?(@.parent_id != null && @.started_on <= " + (System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds + 3600) + " && @.due_on >= " + System.DateTime.UtcNow.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds + ")]");

            if (sprint_milestone == null)

                // Look for a sprint milestone that is not active

                sprint_milestone = sprint_milestones.SelectToken("$..[?(@.parent_id != null && @.start_on <= " + unixTimestamp + " && @.due_on >= " + unixTimestamp + ")]");

            var sprint_name = sprint_milestone["name"].ToString().Split(' ')[0];

            // Jira

            SharedProject.Jira.APIClient JiraClient = new SharedProject.Jira.APIClient("https://janisoncls.atlassian.net");
            JiraClient.User = AppConfig.Get("JiraUser");
            JiraClient.Password = AppConfig.Get("JiraPassword");

            SharedProject.Confluence.APIClient ConfluenceClient = new SharedProject.Confluence.APIClient(AppConfig.Get("ConfluenceUrl"));
            ConfluenceClient.User = AppConfig.Get("ConfluenceUser");
            ConfluenceClient.Password = AppConfig.Get("ConfluenceApiToken");

            //var debug = (JObject)ConfluenceClient.SendGet(AppConfig.Get("ConfluencePage") + "?expand=body.storage");
            //var debug = (JObject)ConfluenceClient.SendGet("2196144403?expand=body.storage");
            //var debug = (JObject)ConfluenceClient.SendGet("997687643/child/page?limit=200");
            //Log.WriteLine(debug.ToString());

            var confluence_page_storage_str = (new CustomPanel(":checkered_flag:", "1f3c1", "#EAE6FF", "Looking for test results? ", "Click here for Titans", "https://janisoncls.atlassian.net/wiki/display/INS/Titans+" + sprint_name + "+QA+Test+Results")).ToStringDisableFormatting();
            confluence_page_storage_str += (new CustomPanel(":hugging:", "1f917", "#E6FCFF", "Help us build better test suites. ", "Click here to submit your ideas", "https://janisoncls.atlassian.net/browse/QD")).ToStringDisableFormatting();
            confluence_page_storage_str += (new CustomPanel(":thinking:", "1f914", "#DEEBFF", "Want to know more? ", "Click here for our Regression Test Engagement Process", "https://janisoncls.atlassian.net/wiki/display/JAST/Regression+Test+Engagement+Process")).ToStringDisableFormatting();
            //var confluence_page_storage_str = (new NotePanel("Looking for test results? ", "Click here for Titans", "https://janisoncls.atlassian.net/wiki/display/INS/Titans+" + sprint_name + "+QA+Test+Results")).ToStringDisableFormatting();
            confluence_page_storage_str += (new Heading3("Table of Contents")).ToStringDisableFormatting();
            confluence_page_storage_str += (new Toc(1, 2)).ToStringDisableFormatting();

            var testrail_plan_type_status_count = new Dictionary<string, int?>();

            Log.WriteLine("Getting case fields ...");
            var case_fields = (JArray)TestRailClient.SendGet("get_case_fields");

            var tags = case_fields.SelectToken("$[?(@.name == 'tags')]");
            var tags_options = tags["configs"][0]["options"]["items"].ToString().TrimEnd('\n');
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

                confluence_page_storage_str += (new Heading1(AppConfig.Get("TestRailUrl") + "/index.php?/projects/overview/" + TestProjectId, TestProjectName)).ToStringDisableFormatting();

                // The test suites

                var abbr_meaning_anchor = new Dictionary<string, string>();
                var suite_num = 0;

                foreach (XmlNode TestSuite in TestProject.SelectNodes("TestSuites/add"))
                {
                    suite_num++;
                    var suite_description_table_rows = new TableRows();
                    var TestSuiteId = TestSuite.GetAttributeValue("Id");

                    Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " \"" + TestProjectName + "\", Suite " + suite_num + " of " + TestProject.SelectNodes("TestSuites/add").Count + " getting details ...");
                    var suite = (JObject)TestRailClient.SendGet("get_suite/" + TestSuiteId);

                    if (suite["description"] != null)
                    {
                        // Reformat TestRail Table markdown to standard markdown and convert to HTML


                        var markdown = suite["description"].ToString();
                        markdown = Regex.Replace(markdown, "^\\|\\|\\|:|\\|:", "|", RegexOptions.Singleline);
                        markdown = Regex.Replace(markdown, "\\r\\n\\|\\|", "\r\n|", RegexOptions.Singleline);
                        markdown = Regex.Replace(markdown, "(^\\|Category.*?\\r\\n)", "$1|---|---|---|---|---|---|---|---|---|\r\n", RegexOptions.Singleline);

                        //var pipeline = new Markdig.MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                        //var suite_description_html = Markdown.ToHtml(markdown, pipeline);
                        //suite_description_html = suite_description_html.Replace("<table>", "<table data-layout=\"full-width\">");
                        //suite_description_html = suite_description_html.Replace("<th>", "<th><sub>");
                        //suite_description_html = suite_description_html.Replace("</th>", "</sub></th>");
                        //suite_description_html = suite_description_html.Replace("<td>", "<td><sub>");
                        //suite_description_html = suite_description_html.Replace("</td>", "</sub></td>");

                        //var markdown = "|:Category|:Abbr / Meaning|:Abbr / Meaning|:Abbr / Meaning|:Abbr / Meaning|:Abbr / Meaning|:Abbr / Meaning|:Abbr / Meaning|:Abbr / Meaning\r\n" +
                        //                "|---|---|---|---|---|---|---|---|---|\r\n" +
                        //                "| Authoring | GMI / Gap Match Interaction | MC / Multiple Choice | KA / Keyword - Automatic | ET / Extended Text | AI / Associate Interaction | COM / Comment | CMP / Composite | DI / Drawing Interaction\r\n" +
                        //                "| Authoring | EPT / Essay - Plain text | ERT / Essay - Rich text | CR / Code response | FU / File Upload | GAI / Graphic Associate Interaction | GGM / Graphic Gap Match Interaction | GOI / Graphic Order Interaction | HI / Hotspot Interaction\r\n" +
                        //                "| Authoring | KSA / Keyword - Semi automatic | MIL / Match Interaction - Draw lines | MIC / Match Interaction - Check box | MID / Match Interaction - Drag and Drop | MCS / Multiple Choices | OI / Order Interaction | POI / Position Object Interaction | SPI / Select Point Interaction\r\n" +
                        //                "| Authoring | SI / Slider Interaction | TSI / Text Spot Interaction | TF / True - False | VID / Video | TQ - Translated Questions | | |\r\n" +
                        //                "| Authoring | ST / Standard Test | BT / Blueprinted Test | SUR - Survey | | | | |\r\n" +
                        //                 "| Delivery | TA / Test Attempt | QUE / Question | SEQ / Sequential | COR / Correct | INC - Incorrect | RND - Random | DR - Disruption | LM - Live Marking\r\n" +
                        //                 "| Delivery | ONL / Online | OFF / Offline | LNB / Low No Bandwidth | TP2 / Test Player 2 | REP / Replay | | |\r\n";

                        var table = MarkdownEx.GetDocument(markdown).GetTable(0);

                        if (table != null)

                            for (int row_num = 0; row_num < table.GetRowCount(); row_num++)
                            {
                                var suite_description_table_row = new TableRow();

                                for (int col_num = 0; col_num < table.GetRow(row_num).GetColumnCount(); col_num++)
                                {
                                    var abbr = "";
                                    var meaning = "";
                                    var suite_description_table_cell = new TableCell();
                                    var cell = table.GetRow(row_num).GetCell(col_num);

                                    if (cell != null)
                                    {
                                        meaning = cell.GetContent().UrlEncode(5);
                                        suite_description_table_cell.Add(meaning);
                                        var next_sibling = cell.GetNextSibling();

                                        if (next_sibling != null)
                                        {
                                            abbr = ((ContainerInline)next_sibling).GetFirstChild().GetContent().UrlEncode(5);
                                            suite_description_table_cell.Add(new Strong(abbr));
                                            meaning = next_sibling.GetNextSibling().GetContent().UrlEncode(5);
                                            suite_description_table_cell.Add(meaning);

                                        }
                                    }

                                    if (row_num >= 1 && col_num > 0)
                                    {
                                        var anchor_text = meaning.Replace("/", "").Trim().Replace(" ", "-");

                                        if (anchor_text.Length > 0)
                                        {
                                            try
                                            {
                                                abbr_meaning_anchor.Add(abbr, anchor_text);
                                            }
                                            catch (Exception e)
                                            {
                                                if (e.Message.Equals("An item with the same key has already been added."))

                                                    Log.WriteLine("A tag with abbreviation " + abbr + " is duplicated.  Fix and try again.");

                                                Environment.Exit(1);
                                            }

                                            suite_description_table_cell.Add(new Anchor(anchor_text));
                                        }
                                    }

                                    suite_description_table_row.Add(suite_description_table_cell);
                                }

                                suite_description_table_rows.Add(suite_description_table_row);
                            }
                    }

                    confluence_page_storage_str += (new Heading2(AppConfig.Get("TestRailUrl") + "/index.php?/projects/overview/" + TestProjectId, suite["name"].ToString())).ToStringDisableFormatting();

                    if (suite_description_table_rows.GetList().Count > 0)
                    {
                        var suite_description_table = new Table("full-width").Add(new TableBody(suite_description_table_rows));
                        confluence_page_storage_str += suite_description_table.ToStringDisableFormatting();
                    }

                    // the test cases

                    var cases_table = new DataTable();
                    cases_table.Columns.Add("ID", typeof(int));
                    cases_table.Columns.Add("Title", typeof(string));
                    cases_table.Columns.Add("Tags", typeof(string));
                    cases_table.Columns.Add("Customers", typeof(string));
                    cases_table.Columns.Add("ScriptName", typeof(string));
                    cases_table.Columns.Add(new DataColumn("Runtime", typeof(int)) { AllowDBNull = true });
                    cases_table.Columns.Add("Task", typeof(string));

                    Log.WriteLine("Prj " + project_num + " of " + AppConfig.GetSectionGroup("TestProjects").GetSectionGroups().Count + " \"" + TestProjectName + "\", Suite " + suite_num + " of " + TestProject.SelectNodes("TestSuites/add").Count + " getting cases ...");
                    var cases = (JObject)TestRailClient.SendGet("get_cases/" + TestProjectId + "&suite_id=" + TestSuiteId);
                    var all_references = "";

                    foreach (var testcase in cases["cases"])
                    {
                        if (case_type_dict[testcase["type_id"].ToString().ToInt()].Contains("Automation"))
                        {
                            var task_key = "";

                            if (testcase["refs"] != null && testcase["refs"].ToString().Length > 0 && testcase["refs"].ToString().StartsWith("INS-"))
                            {
                                task_key = testcase["refs"].ToString();

                                if (all_references.Length > 0)

                                    all_references += ", ";

                                //all_references += "\"" + testcase["refs"] + "\"";
                                all_references += String.Join(", ", testcase["refs"].ToString().Split(',').Select(s => "\"" + s.Trim() + "\""));

                            }

                            var custom_tag_str = "";

                            if (testcase["custom_tags"] != null)

                                foreach (var custom_tag in testcase["custom_tags"])
                                {
                                    if (custom_tag_str.Length > 0)

                                        custom_tag_str = custom_tag_str + ", ";

                                    custom_tag_str = custom_tag_str + tag[custom_tag.ToObject<int>()];
                                }

                            var custom_customer_str = "";

                            if (testcase["custom_customer"] != null)

                                foreach (var custom_customer in testcase["custom_customer"])
                                {
                                    if (custom_customer_str.Length > 0)

                                        custom_customer_str = custom_customer_str + ", ";

                                    custom_customer_str = custom_customer_str + customer[custom_customer.ToObject<int>()];
                                }

                            object custom_runtime = testcase["custom_runtime"];

                            if (((JToken)custom_runtime).IsNullOrEmpty())

                                custom_runtime = DBNull.Value;

                            cases_table.Rows.Add(testcase["id"], testcase["title"], custom_tag_str, custom_customer_str, testcase["custom_auto_script_ref"], custom_runtime, task_key);
                        }
                    }

                    // Jira - get all the tasks from the testrail cases

                    var task_status = new Dictionary<string, string>();

                    if (all_references.Length > 0)
                    {
                        Log.WriteLine("Jira - Getting the software requirements and business requirements via child of links");

                        int startAt = 0;
                        int maxResults = 1000;
                        int totalResults = -1;

                        do
                        {
                            if (totalResults > -1)

                                startAt = startAt + maxResults;

                            //key in ("SP-406", "SP-395", "SP-474", "SP-473", "SP-472", "SP-455", "SP-458", "SP-453", "SP-463", "SP-389", "SP-439", "SP-422", "SP-447", "SP-456", "SP-384", "SP-375", "SP-428", "SP-422")


                            JObject JiraResult = (JObject)JiraClient.SendGet("search?jql=issuetype %3D Task AND key in (" + all_references + ")&maxResults=1000&startAt=" + startAt);
                            //startAt = (int)JiraResult["startAt"];

                            if (JiraResult["maxResults"] != null)
                            {
                                maxResults = (int)JiraResult["maxResults"];
                                totalResults = (int)JiraResult["total"];

                                foreach (dynamic Issue in JiraResult["issues"])
                                {
                                    //                    var SoftReq = SoftReqs.Find(x => x.key == Issue["key"].Value);
                                    var Task = new JiraIssue();
                                    Task.key = Issue["key"].Value;
                                    Task.status = Issue["fields"]["status"]["name"].Value;

                                    task_status.Add(Task.key, Task.status);
                                }
                            }
                        } while ((startAt + maxResults) < totalResults);
                    }



                    cases_table.DefaultView.Sort = "Title ASC";
                    var total_runtime = 0;

                    if (cases_table.DefaultView.ToTable().Rows.Count > 0)
                    {
                        var test_cases_table_rows = new TableRows();

                        test_cases_table_rows.Add(new TableRow(
                            new TableCell(new Bold("[ID] Title")),
                            new TableCell(new Bold("Tags")),
                            new TableCell(new Bold("Customer")),
                            new TableCell(new Bold("Script Name")),
                            new TableCell(new Bold("Runtime")),
                            new TableCell(new Bold("Task"))
                        ));

                        foreach (DataRow row in cases_table.DefaultView.ToTable().Rows)
                        {
                            var title_cell = new TableCell(
                                new Anchor("C" + row.Field<int>(0)),
                                "[",
                                new Hyperlink(AppConfig.Get("TestRailUrl") + "/index.php?/cases/view/" + row.Field<int>(0), "C" + row.Field<int>(0)),
                                "] "
                            );

                            var variant_on_pattern = new Regex("(.*)VARIANT on (\\w+)(.*)");

                            if (variant_on_pattern.IsMatch(row.Field<string>(1).UrlEncode(5)))
                            {
                                title_cell.Add(variant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[1].Value);
                                title_cell.Add("VARIANT on ");
                                var case_id = variant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[2].Value;
                                title_cell.Add(new Hyperlink("#" + case_id, case_id));
                                title_cell.Add(variant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[3].Value);
                            }
                            else
                            {
                                var dependant_on_pattern = new Regex("(.*)DEPENDANT on (\\w+)(.*)");

                                if (dependant_on_pattern.IsMatch(row.Field<string>(1).UrlEncode(5)))
                                {
                                    title_cell.Add(dependant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[1].Value);
                                    title_cell.Add("DEPENDANT on ");
                                    var case_id = dependant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[2].Value;
                                    title_cell.Add(new Hyperlink("#" + case_id, case_id));
                                    title_cell.Add(dependant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[3].Value);
                                }
                                else

                                    title_cell.Add(row.Field<string>(1).UrlEncode(5));
                            }

                            var custom_runtime = "";

                            if (row.Field<int?>(5) != null)

                                custom_runtime = ((int)row.Field<int?>(5)).SecondsToMinutesSecondsString();

                            var coloured_tags_cell = new TableCell("");

                            if (row.Field<string>(2).Trim().Length > 0)
                            {
                                var tags_arr = row.Field<string>(2).SplitAndTrim(',');
                                var multiple_tags = false;

                                // add TagColors to the cell first

                                foreach (XmlNode TagColours in AppConfig.GetSectionGroup("TagsColours").GetSectionGroups())
                                {
                                    var tags_to_colour = TagColours.GetAttributeValue("tags");
                                    var emoji_id = TagColours.GetAttributeValue("value");

                                    var tags_to_colour_arr = tags_to_colour.SplitAndTrim(',');

                                    // if tags to colour is present in tags

                                    if (tags_arr.Count > 0 && tags_to_colour_arr.All(x => tags_arr.Contains(x)))
                                    {
                                        // place the tags to colour in the cell

                                        coloured_tags_cell.Add(new Emoticon(emoji_id));

                                        foreach (var tag_to_colour in tags_to_colour_arr)
                                        {
                                            if (multiple_tags)

                                                coloured_tags_cell.Add(" ");

                                            coloured_tags_cell.Add(new Hyperlink("#" + abbr_meaning_anchor[tag_to_colour], tag_to_colour, true));
                                            multiple_tags = true;
                                            tags_arr.Remove(tag_to_colour);
                                        }
                                    }
                                }

                                // Add remaining tags (uncoloured)

                                foreach (var uncoloured_tag in tags_arr)
                                {
                                    if (multiple_tags)

                                        coloured_tags_cell.Add(", ");

                                    coloured_tags_cell.Add(new Hyperlink("#" + abbr_meaning_anchor[uncoloured_tag], uncoloured_tag, false));
                                    multiple_tags = true;
                                }
                            }

                            total_runtime = total_runtime + (int)(row.Field<int?>(5) == null ? 0 : row.Field<int?>(5));

                            var task_cell = new TableCell("");

                            if (!row.Field<string>(6).Equals("") && task_status.ContainsKey(row.Field<string>(6)))

                                task_cell = new TableCell(
                                    new Hyperlink(AppConfig.Get("JiraUrl") + "/browse/" + row.Field<string>(6), row.Field<string>(6)),
                                    " ",
                                    new Status(task_status[row.Field<string>(6)])
                                );

                            test_cases_table_rows.Add(new TableRow(
                                title_cell,
                                coloured_tags_cell,
                                new TableCell(row.Field<string>(3)),
                                new TableCell(row.Field<string>(4)),
                                new TableCell(custom_runtime),
                                task_cell
                            ));

                        }

                        // TOTAL row

                        test_cases_table_rows.Add(new TableRow(
                            new TableCell(new Bold("TOTAL")),
                            new TableCell(new Bold()),
                            new TableCell(new Bold()),
                            new TableCell(new Bold()),
                            new TableCell(new Bold(total_runtime.SecondsToMinutesSecondsString())),
                            new TableCell(new Bold())
                        ));

                        var test_cases_table = new Table("full-width").Add(
                            new TableColumnGroup(500, 150, 120, 120, 60, 100)).Add(
                            new TableBody(test_cases_table_rows)
                        );

                        confluence_page_storage_str += test_cases_table.ToStringDisableFormatting();
                    }
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
                title = "QA Assessment Regression Test Suites",
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
            int i = 0;

        } 
    }
}
