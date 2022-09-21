
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SharedProject.Confluence
{
    public class Anchor
    {

        public static XNamespace ac = "http://someuri";
        private XElement _Anchor;

        public Anchor(string text, bool clean_text = false)
        {
            if (clean_text)
            {
                const string space_char = "_";
                text = text.Replace("/", "").Trim().Replace(" ", space_char).Replace("|", "-");
                text = Regex.Replace(text, space_char + "+", space_char);
            }

            _Anchor = new XElement(ac + "structured-macro", new XAttribute(XNamespace.Xmlns + "ac", ac.NamespaceName),
                new XAttribute(ac + "name", "anchor"),
                new XElement(ac + "parameter", new XAttribute(XNamespace.Xmlns + "ac", ac.NamespaceName),
                    new XAttribute(ac + "name", ""),
                    text
                )
            );
        }

        public XElement GetXElement()
        {
            return _Anchor;
        }


    }
}
