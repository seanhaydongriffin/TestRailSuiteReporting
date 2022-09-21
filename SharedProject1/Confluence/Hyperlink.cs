
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharedProject.Confluence
{
    public class Hyperlink
    {

        private XElement _Hyperlink;

        public Hyperlink(string url, string text, bool bold = false, bool clean_url = false)
        {
            if (clean_url)
            {
                const string space_char = "_";
                url = url.Replace("/", "").Trim().Replace(" ", space_char).Replace("|", "-");
                url = Regex.Replace(url, space_char + "+", space_char);
            }

            if (bold)

                //_Hyperlink = new XElement("b", new XElement("a", new XAttribute("target", "_blank"), new XAttribute("rel", "noopener noreferrer"), new XAttribute("href", url), text));
                _Hyperlink = new XElement("b", new XElement("a", new XAttribute("href", url), text));
            else

                ///_Hyperlink = new XElement("a", new XAttribute("target", "_blank"), new XAttribute("rel", "noopener noreferrer"), new XAttribute("href", url), text);
                _Hyperlink = new XElement("a", new XAttribute("href", url), text);
        }

        public XElement GetXElement()
        {
            return _Hyperlink;
        }

        public string ToStringDisableFormatting()
        {
            return _Hyperlink.ToString(SaveOptions.DisableFormatting);
        }


    }
}
