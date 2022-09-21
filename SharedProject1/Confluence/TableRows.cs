﻿
using System.Collections.Generic;
using System.Xml.Linq;

namespace SharedProject.Confluence
{
    public class TableRows
    {

        private List<XElement> _TableRows;

        public TableRows()
        {
            _TableRows = new List<XElement>();
        }

        public List<XElement> GetList()
        {
            return _TableRows;
        }

        public TableRows Add(XElement row)
        {
            _TableRows.Add(row);
            return this;
        }

        public TableRows Add(TableRow row)
        {
            _TableRows.Add(row.GetXElement());
            return this;
        }

        public TableRows AddHeader(params string[] fields)
        {
            var row = new XElement("tr");

            foreach (var field in fields)

                row.Add(new XElement("th", new XElement("p", field)));

            _TableRows.Add(row);
            return this;
        }

        //public TableRows Add(List<TableCell> cells)
        //{
        //    var row = new XElement("tr");

        //    foreach (var cell in cells)
        //    {

        //        foreach (var field in cell.GetList())
        //        {
        //            var tmp_field = field;

        //            if (field.GetType() == typeof(TableCell))

        //                tmp_field = ((TableCell)field).GetList();

        //            if (field.GetType() == typeof(Emoticon))

        //                tmp_field = ((Emoticon)field).GetXElement();

        //            if (field.GetType() == typeof(Hyperlink))

        //                tmp_field = ((Hyperlink)field).GetXElement();

        //            row.Add(new XElement("td", new XElement("sub", tmp_field)));
        //        }

        //        //row.Add(new XElement("td", new XElement("p", field)));
        //    }

        //    _TableRows.Add(row);
        //    return this;
        //}

        public TableRows Add(params string[] fields)
        {
            var row = new XElement("tr");

            foreach (var field in fields)

                row.Add(new XElement("td", new XElement("p", field)));

            _TableRows.Add(row);
            return this;
        }

        public TableRows Add(params object[] fields)
        {
            var row = new XElement("tr");

            foreach (var field in fields)
            {
                var tmp_field = field;

                if (field.GetType() == typeof(TableCell))

                    tmp_field = ((TableCell)field).GetList();

                if (field.GetType() == typeof(Emoticon))

                    tmp_field = ((Emoticon)field).GetXElement();

                if (field.GetType() == typeof(Hyperlink))

                    tmp_field = ((Hyperlink)field).GetXElement();

                row.Add(new XElement("td", new XElement("sub", tmp_field)));
            }

            _TableRows.Add(row);
            return this;
        }


    }
}
