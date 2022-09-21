
using System.Collections.Generic;
using System.Xml.Linq;

namespace SharedProject.Confluence
{
    public class TableCell
    {

        private List<object> _TableCell;

        public TableCell()
        {
            _TableCell = new List<object>();
        }

        public TableCell(params object[] fields)
        {
            _TableCell = new List<object>();

            foreach (var field in fields)
            {
                var tmp_field = field;

                if (field.GetType() == typeof(Anchor))

                    tmp_field = ((Anchor)field).GetXElement();

                if (field.GetType() == typeof(Hyperlink))

                    tmp_field = ((Hyperlink)field).GetXElement();

                if (field.GetType() == typeof(Status))

                    tmp_field = ((Status)field).GetXElement();

                if (field.GetType() == typeof(Bold))

                    tmp_field = ((Bold)field).GetXElement();

                _TableCell.Add(tmp_field);
            }
        }

        public TableCell Add(object field)
        {
            if (field.GetType() == typeof(Anchor))

                field = ((Anchor)field).GetXElement();

            if (field.GetType() == typeof(Hyperlink))

                field = ((Hyperlink)field).GetXElement();

            if (field.GetType() == typeof(Emoticon))

                field = ((Emoticon)field).GetXElement();

            _TableCell.Add(field);
            return this;
        }

        //public List<object> GetList()
        //{
        //    return _TableCell;
        //}

        public List<object> GetList()
        {

            var tmp = new List<object>();

            foreach (var field in _TableCell)
            {
                var tmp_field = field;

                if (field.GetType() == typeof(Anchor))

                    tmp_field = ((Anchor)field).GetXElement();

                if (field.GetType() == typeof(Hyperlink))

                    tmp_field = ((Hyperlink)field).GetXElement();

                if (field.GetType() == typeof(Strong))

                    tmp_field = ((Strong)field).GetXElement();

                tmp.Add(tmp_field);
            }

            return tmp;
        }

        public int GetCount()
        {
            return _TableCell.Count;
        }


    }
}
