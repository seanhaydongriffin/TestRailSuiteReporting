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
    class Program
    {
        private static SharedProject.TestRail.APIClient TestRailAPIClient = null;
        private static SharedProject.TestRail.WebClient TestRailWebClient = null;

        static void Main(string[] args)
        {

            Log.Initialise(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\TestRailSuiteReporting.log");
            Log.Initialise(null);
            AppConfig.Open();

            TestRailAPIClient = new SharedProject.TestRail.APIClient(AppConfig.Get("TestRailUrl"));
            TestRailAPIClient.User = AppConfig.Get("TestRailUser");
            TestRailAPIClient.Password = AppConfig.Get("TestRailPassword");

            TestRailWebClient = new SharedProject.TestRail.WebClient(AppConfig.Get("TestRailUrl"));
            TestRailWebClient.User = AppConfig.Get("TestRailUser");
            TestRailWebClient.Password = AppConfig.Get("TestRailPassword");

            Log.WriteLine("TestRail login ...");
            TestRailWebClient.Login();

            var today = System.DateTime.Today;
            var unixTimestamp = (int)today.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;
            Log.WriteLine("today = " + today);
            Log.WriteLine("unixTimestamp = " + unixTimestamp);

            Log.WriteLine("Getting milestones for TestRail Janison Assessment project ...");
            var milestones = (JObject)TestRailAPIClient.SendGet("get_milestones/" + 64);

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
            var case_id_title = new Dictionary<string, string>();

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
                    //var suite = (JObject)TestRailClient.SendGet("get_suite/" + TestSuiteId);
                    var suite_xml = TestRailWebClient.SendGet("suites/export/" + TestSuiteId);

                    var suite = suite_xml.GetChildNode("suite");
                    var suite_id = suite.GetChildNode("id").InnerText.Replace("S", "");
                    var suite_name = suite.GetChildNode("name").InnerText;
                    var suite_description = suite.GetChildNode("description").InnerText;

                    if (suite_description.Length > 0)
                    {
                        // Reformat TestRail Table markdown to standard markdown and convert to HTML

                        var markdown = suite_description;
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

                    confluence_page_storage_str += (new Heading2(AppConfig.Get("TestRailUrl") + "/index.php?/suites/view/" + suite_id, suite_name)).ToStringDisableFormatting();

                    if (suite_description_table_rows.GetList().Count > 0)
                    {
                        var suite_description_table = new Table("full-width").Add(new TableBody(suite_description_table_rows));
                        confluence_page_storage_str += suite_description_table.ToStringDisableFormatting();
                    }

                    // the test cases

                    var cases_table = new DataTable();
                    cases_table.Columns.Add("ID", typeof(string));
                    cases_table.Columns.Add("Title", typeof(string));
                    cases_table.Columns.Add("Tags", typeof(string));
                    cases_table.Columns.Add("Customers", typeof(string));
                    cases_table.Columns.Add("ScriptName", typeof(string));
                    cases_table.Columns.Add("Runtime", typeof(string));
                    cases_table.Columns.Add("Task", typeof(string));

                    var all_references = "";
                    var sections = Xml.GetChildNodes(suite, "sections/section");

                    foreach (XmlNode section in sections)
                    {
                        var cases = Xml.GetChildNodes(section, "cases/case");

                        foreach (XmlNode testcase in cases)
                        {
                            var id = testcase.GetChildNode("id").InnerText.Replace("C", "");
                            var title = testcase.GetChildNode("title").InnerText;
                            var type = testcase.GetChildNode("type").InnerText;
                            var references = testcase.GetChildNode("references").InnerText;
                            var custom = testcase.GetChildNode("custom");
                            var auto_script_ref = custom.GetChildNode("auto_script_ref");
                            var tags = custom.GetChildNode("tags");
                            var customer = custom.GetChildNode("customer");
                            var runtime = custom.GetChildNode("runtime");

                            if (type.Contains("Automation"))
                            {
                                if (references.Length > 0 && (new Regex("^[A-Z][A-Z][A-Z]-.+")).IsMatch(references))
                                {
                                    if (all_references.Length > 0)

                                        all_references += ", ";

                                    all_references += String.Join(", ", references.Split(',').Select(s => "\"" + s.Trim() + "\""));
                                }

                                var custom_tag_str = "";

                                if (tags != null)
                                {
                                    var tags2 = Xml.GetChildNodes(tags, "item");

                                    foreach (XmlNode each_tag in tags2)
                                    {
                                        if (custom_tag_str.Length > 0)

                                            custom_tag_str += ", ";

                                        custom_tag_str += each_tag.GetChildNode("value").InnerText;
                                    }
                                }

                                var custom_customer_str = "";

                                if (customer != null)
                                {
                                    var customers = Xml.GetChildNodes(customer, "item");

                                    foreach (XmlNode each_customer in customers)
                                    {
                                        if (custom_customer_str.Length > 0)

                                            custom_customer_str += ", ";

                                        custom_customer_str += each_customer.GetChildNode("value").InnerText;
                                    }
                                }

                                var auto_script_ref_str = "";

                                if (auto_script_ref != null)

                                    auto_script_ref_str = auto_script_ref.InnerText;

                                var custom_runtime = "";

                                if (runtime != null)

                                    custom_runtime = runtime.InnerText;

                                case_id_title.Add(id, title);

                                cases_table.Rows.Add(id, title, custom_tag_str, custom_customer_str, auto_script_ref_str, custom_runtime, references);
                            }

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
                            var anchor_text = "C" + row.Field<string>(0) + "_-_" + case_id_title[row.Field<string>(0)];

                            var title_cell = new TableCell(
                                new Anchor(anchor_text, true),
                                //new Anchor("C" + row.Field<string>(0)),
                                "[",
                                new Hyperlink(AppConfig.Get("TestRailUrl") + "/index.php?/cases/view/" + row.Field<string>(0), "C" + row.Field<string>(0)),
                                "] "
                            );

                            var variant_on_pattern = new Regex("(.*)VARIANT on (\\w+)(.*)");

                            if (variant_on_pattern.IsMatch(row.Field<string>(1).UrlEncode(5)))
                            {
                                title_cell.Add(variant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[1].Value);
                                title_cell.Add("VARIANT on ");
                                var case_id = variant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[2].Value;
//                                title_cell.Add(new Hyperlink("#" + case_id, case_id));

                                if (case_id_title.ContainsKey(case_id.Replace("C", "")))

                                    title_cell.Add(new Hyperlink("#" + case_id + "_-_" + case_id_title[case_id.Replace("C", "")], case_id, false, true));
                                else

                                    title_cell.Add(case_id);

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
                                    //                                    title_cell.Add(new Hyperlink("#" + case_id, case_id));

                                    if (case_id_title.ContainsKey(case_id.Replace("C", "")))

                                        title_cell.Add(new Hyperlink("#" + case_id + "_-_" + case_id_title[case_id.Replace("C", "")], case_id, false, true));
                                    else

                                        title_cell.Add(case_id);

                                    title_cell.Add(dependant_on_pattern.Match(row.Field<string>(1).UrlEncode(5)).Groups[3].Value);
                                }
                                else

                                    title_cell.Add(row.Field<string>(1).UrlEncode(5));
                            }

                            var custom_runtime = "";

                            if (row.Field<string>(5) != null && row.Field<string>(5).Length > 0)

                                custom_runtime = row.Field<string>(5).ToInt().SecondsToMinutesSecondsString();

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

                            total_runtime = total_runtime + (row.Field<string>(5) == null ? 0 : row.Field<string>(5).ToInt());

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
