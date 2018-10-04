using System.Configuration;
using System.Runtime.Serialization;

namespace Reindexer.Helpers
{
    [DataContract()]
    public class ConfigSection : ConfigurationSection
    {
        [IgnoreDataMember()]
        public static ConfigSection Default
        {
            get { return (ConfigSection)ConfigurationManager.GetSection("reindexer/console"); }
        }

        [IgnoreDataMember]
        [ConfigurationProperty("node")]
        public NodeConfigurationElement Node
        {
            get { return (NodeConfigurationElement)this["node"]; }
            set { this["node"] = value; }
        }

        [IgnoreDataMember]
        [ConfigurationProperty("index")]
        public IndexConfigurationElement Index
        {
            get { return (IndexConfigurationElement)this["index"]; }
            set { this["index"] = value; }
        }

        [IgnoreDataMember]
        [ConfigurationProperty("date")]
        public DateConfigurationElement Date
        {
            get { return (DateConfigurationElement)this["date"]; }
            set { this["date"] = value; }
        }

        [IgnoreDataMember]
        [ConfigurationProperty("bulk")]
        public BulkConfigurationElement Bulk
        {
            get { return (BulkConfigurationElement)this["bulk"]; }
            set { this["bulk"] = value; }
        }

        [DataContract]
        public class NodeConfigurationElement : ConfigurationElement
        {
            [DataMember]
            [ConfigurationProperty("from", IsRequired = true)]
            public string From
            {
                get { return (string)this["from"]; }
                set { this["from"] = value; }
            }

            [DataMember]
            [ConfigurationProperty("to", IsRequired = true)]
            public string To
            {
                get { return (string)this["to"]; }
                set { this["to"] = value; }
            }
        }

        [DataContract]
        public class IndexConfigurationElement : ConfigurationElement
        {
            [DataMember]
            [ConfigurationProperty("from", IsRequired = true)]
            public string From
            {
                get { return (string)this["from"]; }
                set { this["from"] = value; }
            }

            [DataMember]
            [ConfigurationProperty("to", IsRequired = true)]
            public string To
            {
                get { return (string)this["to"]; }
                set { this["to"] = value; }
            }
        }

        [DataContract]
        public class DateConfigurationElement : ConfigurationElement
        {
            [DataMember]
            [ConfigurationProperty("from", IsRequired = true)]
            public string From
            {
                get { return (string)this["from"]; }
                set { this["from"] = value; }
            }

            [DataMember]
            [ConfigurationProperty("to", IsRequired = true)]
            public string To
            {
                get { return (string)this["to"]; }
                set { this["to"] = value; }
            }
        }

        [DataContract]
        public class BulkConfigurationElement : ConfigurationElement
        {
            [DataMember]
            [ConfigurationProperty("size", IsRequired = false, DefaultValue = 500)]
            public int Size
            {
                get { return (int)this["size"]; }
                set { this["size"] = value; }
            }

            [DataMember]
            [ConfigurationProperty("scroll", IsRequired = false, DefaultValue = "1m")]
            public string Scroll
            {
                get { return (string)this["scroll"]; }
                set { this["scroll"] = value; }
            }
        }
    }
}