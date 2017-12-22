using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace XmlUtilities
{
	class Program
	{
		static void Main(string[] args)
		{
			string labelsCommaSeparated = "Advertisements,Family,Friends,Financial,Tech Information,Work,Grace Ross,Mike,Google Voice,Education,Social,Me,News,Financial/Rent,Organizations,Appartments,Political,School,Food,Calendar,Legal,Privacy,Music,Car,New Jobs,Jobs,Travel,Craigslist,Appartments/Moving,Creative,Development,Health,Iris";
			string filepath = @"C:\Users\Justin Ross\Desktop\mailFilters.xml";
			var labels = labelsCommaSeparated.Split(',').Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim().ToLower());

			XmlDocument doc = new XmlDocument();
			doc.PreserveWhitespace = false;
			doc.Load(filepath);
			var newFileNameNoExt = GetFullPathWithoutExtension(filepath)
								   + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss");
			var newFileName = newFileNameNoExt + Path.GetExtension(filepath);
			var newFileNameLog = newFileNameNoExt + "_LOG.txt";
			Logger l = new Logger(newFileNameLog);
			l.LogH1("Combine Label Filter Duplicates");
			l.Log("...with FROM email addresses specified");
			foreach (var label in labels)
			{
				RemoveDuplicateLabelFilters_FiltersBasedOnEmailAddressFrom(doc, label, l);
			}



			l.Log("writing file");

			doc.Save(newFileName);


			var xPath = "//apps:feed[1]";


		}
		public class Logger
		{
			public string FilePath { get; }

			private string h1(string title) =>
				Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ "-------------------------------------------------------"
				+ Environment.NewLine
				+ title.ToUpper()
				+ Environment.NewLine
				+ "-------------------------------------------------------"
				+ Environment.NewLine;

			private string h2(string title) =>
				Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ Environment.NewLine
				+ "----------"
				+ Environment.NewLine
				+ title.ToUpper()
				+ Environment.NewLine
				+ "----------"
				+ Environment.NewLine;

			private string h3(string title) =>
				Environment.NewLine
				+ Environment.NewLine
				+ title
				+ Environment.NewLine;

			private string h4(string title) =>
				Environment.NewLine
				+ title;


			public Logger(string filePath)
			{
				FilePath = filePath;
			}
			public void Log(string message)
			{
				System.IO.File.AppendAllLines(this.FilePath, new List<string>() { message }); ;
			}

			public void LogH1(string message)
			{
				System.IO.File.AppendAllText(this.FilePath, h1(message)); ;

			}

			public void LogH2(string message)
			{
				System.IO.File.AppendAllText(this.FilePath, h2(message)); ;

			}

			public void LogH3(string message)
			{
				System.IO.File.AppendAllText(this.FilePath, h3(message)); ;

			}

			public void LogH4(string message)
			{
				System.IO.File.AppendAllText(this.FilePath, h4(message)); ;

			}


			public void Log(IEnumerable<string> messages)
			{
				System.IO.File.AppendAllLines(this.FilePath, messages); ;
			}


		}
		

		private static int googleMaxLengthOfFilter = 1520;
		public static void RemoveDuplicateLabelFilters_FiltersBasedOnEmailAddressFrom(XmlDocument doc, string label, Logger l)
		{
			l.LogH2($"Label {label}");

			var elements = doc.GetElementsByTagName("entry");
			StringBuilder sb = new StringBuilder();
			var nodes = new List<XmlNode>();
			foreach (var entry in elements)
			{

				var nodeEntry = ((XmlNode)entry);
				bool matchesFrom = false;
				bool matchesLabel = false;
				string tempFrom = string.Empty;

				//look through child nodes of Entry element
				foreach (var n in nodeEntry.ChildNodes)
				{
					var nodeChild = ((XmlNode)n);
					if (nodeChild.Attributes == null) continue;
					//loop through attributes of each child node
					var attr = (XmlAttribute)nodeChild.Attributes["name"];
					if (attr == null || (attr.Name != "name" && attr.Value != "label" && attr.Value != "from")) continue;
					if (attr.Value == "from")
					{
						matchesFrom = true;
						var attrAd = (XmlAttribute)(nodeChild.Attributes["value"]);
						if (!string.IsNullOrWhiteSpace(attrAd.Value))
						{
							tempFrom += " OR " + attrAd.Value;
							matchesFrom = true;
						}
					}

					if (attr.Value == "label")
					{
						var attrAd = (XmlAttribute)(nodeChild.Attributes["value"]);
						if (!string.IsNullOrWhiteSpace(attrAd.Value)
							&& string.Equals(attrAd.Value, label, StringComparison.CurrentCultureIgnoreCase))
						{
							matchesLabel = true;
						}
					}

				}
				if (matchesFrom && matchesLabel)
				{
					l.Log($"Found {tempFrom.TrimStart(" OR ".ToCharArray())}");

					sb.Append(tempFrom);
					nodes.Add((XmlNode)entry);
				}

			}

			l.LogH3($"Results Matching '{label}': {nodes.Count}");


			if (nodes.Count > 0)
			{
				var first = nodes[0];
				var parent = first.ParentNode;
				var toCombine = nodes.GetRange(1, nodes.Count - 1);
				var nodeToobig = false;
				var newValue = string.Join(" OR ", sb.ToString().TrimStart(" OR ".ToCharArray()).Split(" OR ").OrderBy(s=>s));
				l.LogH3($"Combining {toCombine.Count} with the first one");

				foreach (var node in toCombine)
				{
					parent.RemoveChild(node);
				}

				SetLabelFilterFromValue(first, newValue, l);
				var nodesToAdd = SplitFiltersBasedOffOfFromValue(first, l, "filter_with_label_" + label.Replace(" ", string.Empty) + "_@part.{number}.com", " OR ");
				foreach (var node in nodesToAdd)
				{
					parent.InsertAfter(node, first);

				}
				parent.RemoveChild(first);

				//if (newValue.Length > googleMaxLengthOfFilter)
				//{
				//	var nodesToAdd = SplitFiltersBasedOffOfFromValue(first, l, " OR ");
				//	foreach (var node in nodesToAdd)
				//	{
				//		parent.InsertAfter(node, first);

				//	}
				//	parent.RemoveChild(first);
				//}
			}

			l.LogH3($"'{label}' DONE!");

		}

		private static List<XmlNode> SplitFiltersBasedOffOfFromValue(XmlNode node, Logger l, string labelTitle, string splitOn = " OR ")
		{
			XmlAttribute attr = GetFromValueAttribute(node);
			int separtorOffset = splitOn.Length + labelTitle.Length;
			var toReturn = new List<XmlNode>();
			string originalVal = attr.Value;

			var allDivided = new Stack<string>(originalVal.Split(splitOn).OrderBy(s=>s).Reverse());

			string current = null;
			var currentSb = new StringBuilder();
			var sbs = new List<StringBuilder>(){ currentSb };

			while (allDivided.TryPop(out current))
			{
				int newLength = currentSb.Length + current.Length + separtorOffset;
				if (newLength >= googleMaxLengthOfFilter)
				{
					//line is full
					currentSb = new StringBuilder();
					sbs.Insert(0, currentSb);

				}

				currentSb.Append(current);
				currentSb.Append(splitOn);
			}

			int newElementCount = sbs.Count;

			for (int i = 0; i < newElementCount; i++)
			{
				var number = newElementCount - i;
				string newValue = 
					labelTitle.Replace("{number}", number.ToString()).ToUpperInvariant()
					+ splitOn 
					+ sbs[i].ToString().Trim(splitOn.ToCharArray()).Trim();

				var newNode = node.Clone();
				SetLabelFilterFromValue((XmlNode)newNode, newValue, l);
				toReturn.Add(newNode);

			}


			return toReturn;

		}


		private static void SetLabelFilterFromValue(XmlNode filterEntry, string newValue, Logger l)
		{
			foreach (var n in filterEntry.ChildNodes)
			{
				var node = (XmlNode)n;
				var attrFrom = (XmlAttribute)(node.Attributes?["name"]);
				if (attrFrom == null || attrFrom.Value != "from") continue;
				l.LogH3($"Setting the filter 'FROM' Value to the following: {newValue}");

				node.Attributes["value"].Value = newValue;
				if (newValue.Length > googleMaxLengthOfFilter)
				{
					l.LogH3($"Warning! The 'FROM' is too long. It will be broken into multiple filters.");

				}


				break;
			}

		}

		private static XmlAttribute GetFromValueAttribute(XmlNode filterEntry)
		{

			/*
			 *  <entry>
					<apps:property name="from" value="jobs@theladders.com OR noreply@okta.com" />
				</entry>

			this function returns ->  value="jobs@theladders.com OR noreply@okta.com"
			 */
			foreach (var n in filterEntry.ChildNodes)
			{
				var node = (XmlNode)n;
				var attrFrom = (XmlAttribute)(node.Attributes?["name"]);
				if (attrFrom == null || attrFrom.Value != "from") continue;
				return node.Attributes["value"];
			}
			return null;
		}

		public static String GetFullPathWithoutExtension(String path)
		{
			return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path));
		}
		public static XmlNamespaceManager GetXmlNamespaceManager(string filePath)
		{


			XDocument xml = XDocument.Load(filePath);
			//XDocument xml = XDocument.Load(@"C:\Users\Justin Ross\Desktop\mailFilters.xml");
			//feed/entry[1]/apps:property[2]@value
			var result = xml.Root?.Attributes().
			 Where(a => a.IsNamespaceDeclaration).
			 GroupBy(a => a.Name.Namespace == XNamespace.None ? String.Empty : a.Name.LocalName,
			  a => XNamespace.Get(a.Value)).
			 ToDictionary(g => g.Key,
			  g => g.First()).ToList();

			var namespaceResolver = new XmlNamespaceManager(new NameTable());

			result.ForEach(kv =>
			{
				if (string.IsNullOrWhiteSpace(kv.Key)) return;
				namespaceResolver.AddNamespace(kv.Key, kv.Value.NamespaceName);
			});


			return namespaceResolver;
		}

		static string FindXPath(XmlNode node)
		{
			StringBuilder builder = new StringBuilder();
			while (node != null)
			{
				switch (node.NodeType)
				{
					case XmlNodeType.Attribute:
						builder.Insert(0, "/@" + node.Name);
						node = ((XmlAttribute)node).OwnerElement;
						break;
					case XmlNodeType.Element:
						int index = FindElementIndex((XmlElement)node);
						builder.Insert(0, "/" + node.Name + "[" + index + "]");
						node = node.ParentNode;
						break;
					case XmlNodeType.Document:
						return builder.ToString();
					default:
						throw new ArgumentException("Only elements and attributes are supported");
				}
			}
			throw new ArgumentException("Node was not in a document");
		}

		static int FindElementIndex(XmlElement element)
		{
			XmlNode parentNode = element.ParentNode;
			if (parentNode is XmlDocument)
			{
				return 1;
			}
			XmlElement parent = (XmlElement)parentNode;
			int index = 1;
			foreach (XmlNode candidate in parent.ChildNodes)
			{
				if (candidate is XmlElement && candidate.Name == element.Name)
				{
					if (candidate == element)
					{
						return index;
					}
					index++;
				}
			}
			throw new ArgumentException("Couldn't find element within parent");
		}
	}
}



