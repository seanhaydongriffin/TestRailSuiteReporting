
using System.Xml.Linq;

namespace SharedProject.Confluence
{
    public class Anchor
    {

        public static XNamespace ac = "http://someuri";
        private XElement _Anchor;

        public Anchor(string text)
        {
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
