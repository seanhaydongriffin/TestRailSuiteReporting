
using System.Xml.Linq;

namespace SharedProject.Confluence
{
    public class Heading3
    {

        private XElement _Heading3;

        public Heading3(string text)
        {
            _Heading3 = new XElement("h3", text);
        }

        public Heading3(string HyperlinkUrl, string HyperlinkText)
        {
            _Heading3 = new XElement("h2",
                new XElement("u",
                    new XElement("a",
                        new XAttribute("href", HyperlinkUrl),
                        HyperlinkText
                    )
                )
            );
        }

        public XElement GetXElement()
        {
            return _Heading3;
        }


        public string ToStringDisableFormatting()
        {
            return _Heading3.ToString(SaveOptions.DisableFormatting);
        }

    }
}
