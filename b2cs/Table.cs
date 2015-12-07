using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace b2cs
{
    public class Table
    {
        // Set this to skip all the "table" stuff and just dump the output.
        public static bool PrintRaw = false;

        public Table(params string[] headings)
        {
            m_padLeft = new List<bool>();
            m_items = new List<List<string>>();

            var item = new List<string>();
            foreach (string heading in headings)
            {
                if (heading.StartsWith("_"))
                {
                    m_padLeft.Add(true);
                    item.Add(heading.Substring(1));
                }
                else
                {
                    m_padLeft.Add(false);
                    item.Add(heading);
                }
            }
            m_items.Add(item);
        }

        public void Add(params string[] values)
        {
            if (values.Length != m_items[0].Count)
            {
                throw new ArgumentException("Not enough values.");
            }

            m_items.Add(new List<string>(values));
        }

        public void Print()
        {
            if (m_items.Count == 0)
                return;

            if (PrintRaw)
            {
                foreach (List<string> item in m_items.Skip(1))
                {
                    Console.WriteLine(string.Join("\t", item));
                }
                return;
            }

            var measures = new int[m_items[0].Count];

            foreach (List<string> item in m_items)
            {
                for (int i = 0; i < measures.Length; i++)
                {
                    if (item[i] != null)
                    {
                        measures[i] = Math.Max(measures[i], item[i].Length);
                    }
                    else
                    {
                        item[i] = string.Empty;
                    }
                }
            }

            bool isHeader = true;
            foreach (List<string> item in m_items)
            {
                var padded = new List<string>();
                for (int i = 0; i < item.Count; i++)
                {
                    if (isHeader || !m_padLeft[i])
                    {
                        padded.Add(item[i].PadRight(measures[i], ' '));
                    }
                    else
                    {
                        padded.Add(item[i].PadLeft(measures[i], ' '));
                    }
                }

                string line = string.Join(" | ", padded);
                Console.WriteLine(line);

                if (isHeader)
                {
                    Console.WriteLine(new string('-', line.Length));
                    isHeader = false;
                }
            }
        }

        private List<bool> m_padLeft;
        private List<List<string>> m_items;
    }
}
