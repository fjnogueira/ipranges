﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Xml;

namespace IpRanges
{
    public static class IPRangesParser
    {
        public static IEnumerable<IPRangesGroup> ParseFromResources(string resourcePrefix = null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string[] names;
                try { names = assembly.GetManifestResourceNames(); }
                catch { continue; }

                foreach (var resName in names)
                {
                    if (!String.IsNullOrEmpty(resourcePrefix))
                    {
                        if (!resName.StartsWith(resourcePrefix)) continue;
                    }

                    if (!resName.EndsWith(".xml")) continue;

                    IPRangesGroup group;
                    try
                    {
                        group = ParseFromXml(assembly.GetManifestResourceStream(resName));
                    }
                    catch (XmlException)
                    {
                        continue;
                    }
                    yield return group;
                }
            }
        }

        public static IPRangesGroup ParseFromXml(string xml)
        {
            if (xml == null) throw new ArgumentNullException("xml");

            using (var mem = new MemoryStream(Encoding.Default.GetBytes(xml)))
            using (var reader = new StreamReader(mem))
                return ParseFromXml(reader.BaseStream);
        }

        public static IPRangesGroup ParseFromXml(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException("stream");

            using (var reader = new XmlTextReader(stream))
                return ParseFromXml(reader);
        }

        public static IPRangesGroup ParseFromXml(XmlTextReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");

            IPRangesGroup group = null;
            IPRangesRegion region = null;

            var level = 0;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element:
                        level++;
                        try
                        {
                            var name = reader.Name.ToLowerInvariant();
                            if (level == 1)
                            {
                                if (name == "group")
                                {
                                    group = ReadGroupElement(reader);
                                    continue;
                                }
                                throw new XmlException("Invalid root element('" + reader.Name + "'), expecting 'group'");
                            }

                            if (level == 2 && name == "region")
                            {
                                if (group == null) throw new InvalidDataException("Missing appropriate root element");
                                region = ReadRegionElement(reader);
                                region.ParentGroup = group;
                                group.Regions.Add(region);
                                continue;
                            }

                            if (level == 3 && name == "range")
                            {
                                if (group == null) throw new InvalidDataException("Missing 'group' element");
                                if (region == null) throw new InvalidDataException("Missing 'region' element");

                                region.Ranges.Add(ReadRangeElement(reader));
                            }
                        }
                        finally
                        {
                            if (reader.IsEmptyElement) level--;
                        }
                        break;
                    case XmlNodeType.EndElement:
                        level--;
                        if (level == 0) return group;
                        break;
                }
            }

            return group;
        }

        private static IPRangesGroup ReadGroupElement(XmlReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");

            var group = new IPRangesGroup();
            while (reader.MoveToNextAttribute())
            {
                var attrName = reader.Name.ToLowerInvariant();
                if (attrName == "name") @group.Name = reader.Value;
            }
            reader.MoveToContent();
            return group;
        }

        private static IPRangesRegion ReadRegionElement(XmlReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");

            var region = new IPRangesRegion();
            while (reader.MoveToNextAttribute())
            {
                var attrName = reader.Name.ToLowerInvariant();
                switch (attrName)
                {
                    case "name": region.Name = reader.Value.Trim(); break;
                    case "description": region.Description = reader.Value; break;
                }
            }
            reader.MoveToContent();
            return region;
        }

        private static IPRange ReadRangeElement(XmlReader reader)
        {
            if (reader == null) throw new ArgumentNullException("reader");

            string network = null;
            string from = null;
            string to = null;
            while (reader.MoveToNextAttribute())
            {
                var attrName = reader.Name.ToLowerInvariant();
                switch (attrName)
                {
                    case "network": network = reader.Value.Trim(); break;
                    case "from": from = reader.Value.Trim(); break;
                    case "to": to = reader.Value.Trim(); break;
                }
            }

            IPRange range = null;
            IPAddress fromIp = null;
            IPAddress toIp = null;

            if (!String.IsNullOrEmpty(network)) range = IPRange.Parse(network);
            if (!String.IsNullOrEmpty(from))
            {
                if (!IPAddress.TryParse(from, out fromIp))
                    throw new FormatException(String.Format("An invalid from IP address was specified ('{0}').", from));
                if (range != null && !fromIp.Equals(range.From))
                    throw new InvalidDataException(String.Format("From IP in range does not match calculated value, data seems to be inconsistent ({0} != {1})", fromIp, range.From));
            }
            if (!String.IsNullOrEmpty(to))
            {
                if (!IPAddress.TryParse(to, out toIp))
                    throw new FormatException(String.Format("An invalid to IP address was specified ('{0}').", to));
                if (range != null && !toIp.Equals(range.To))
                    throw new InvalidDataException(String.Format("To IP in range does not match calculated value, data seems to be inconsistent ({0} != {1})", toIp, range.To));
            }

            if (range == null)
            {
                if (fromIp == null) throw new InvalidDataException("Missing 'from' or 'network' attribute for range");
                if (toIp == null) throw new InvalidDataException("Missing 'to' or 'network' attribute for range");
                range = new IPRange(fromIp, toIp);
            }
            reader.MoveToContent();
            return range;
        }
    }
}