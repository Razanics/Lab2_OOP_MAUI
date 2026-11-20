using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Xsl;

namespace Lab2_OOP_MAUI;

public class SearchCriteria
{
    public string? FullName { get; set; }
    public string? Faculty { get; set; }
    public string? Department { get; set; }
    public string? Course { get; set; }
    public string? SubjectName { get; set; }
    public string? Keyword { get; set; }

    public string ToXPathQuery()
    {
        var predicates = new List<string>();
        if (!string.IsNullOrWhiteSpace(FullName))
            predicates.Add($"@FullName='{FullName}'");
        if (!string.IsNullOrWhiteSpace(Faculty))
            predicates.Add($"@Faculty='{Faculty}'");
        if (!string.IsNullOrWhiteSpace(Department))
            predicates.Add($"@Department='{Department}'");
        if (!string.IsNullOrWhiteSpace(Course))
            predicates.Add($"@Course='{Course}'");
        if (!string.IsNullOrWhiteSpace(SubjectName))
            predicates.Add($"Subject[@Name='{SubjectName}']");

        return predicates.Count > 0
            ? $"/UniversityDatabase/Student[{string.Join(" and ", predicates)}]"
            : "/UniversityDatabase/Student";
    }

    public override string ToString() =>
        $"FullName={FullName ?? "Усі"}, Faculty={Faculty ?? "Усі"}, Department={Department ?? "Усі"}, Course={Course ?? "Усі"}, Subject={SubjectName ?? "Усі"}, Keyword={Keyword ?? "Немає"}";
}

public class StudentRecord
{
    public string FullName { get; set; } = "Не вказано";
    public string Faculty { get; set; } = "Не вказано";
    public string Department { get; set; } = "Не вказано";
    public string Course { get; set; } = "Не вказано";
    public string Group { get; set; } = "Не вказано";
    public string Form { get; set; } = "Не вказано";
    public List<(string Name, string Grade, string Credits)> Subjects { get; set; } = new();

    public override string ToString()
    {
        string subjects = Subjects.Count > 0
            ? string.Join("; ", Subjects.Select(s => $"{s.Name}: {s.Grade} б. ({s.Credits} кр.)"))
            : "Немає дисциплін";
        return $"• П.І.П.: {FullName} | Факультет: {Faculty} | Кафедра: {Department} | Курс: {Course} | Група: {Group} | Форма: {Form}\n  Дисципліни: {subjects}";
    }
}

public interface IXmlSearchStrategy
{
    string StrategyName { get; }
    List<StudentRecord> Search(string xmlPath, SearchCriteria criteria);
}

public class SaxSearchStrategy : IXmlSearchStrategy
{
    public string StrategyName => "SAX API (XmlTextReader)";

    public List<StudentRecord> Search(string xmlPath, SearchCriteria criteria)
    {
        var results = new List<StudentRecord>();
        StudentRecord? current = null;

        var xmlReader = new XmlTextReader(xmlPath);
        try
        {
            while (xmlReader.Read())
            {
                switch (xmlReader.NodeType)
                {
                    case XmlNodeType.Element:
                        string elemName = xmlReader.Name;

                        if (elemName == "Student" && xmlReader.HasAttributes)
                        {
                            string fullName = "", faculty = "", department = "", course = "";
                            string studentGroup = "Не вказано", studentForm = "Не вказано";

                            while (xmlReader.MoveToNextAttribute())
                            {
                                switch (xmlReader.Name)
                                {
                                    case "FullName": fullName = xmlReader.Value; break;
                                    case "Faculty": faculty = xmlReader.Value; break;
                                    case "Department": department = xmlReader.Value; break;
                                    case "Course": course = xmlReader.Value; break;
                                    case "Group": studentGroup = xmlReader.Value; break;
                                    case "Form": studentForm = xmlReader.Value; break;
                                }
                            }

                            current = new StudentRecord
                            {
                                FullName = fullName,
                                Faculty = faculty,
                                Department = department,
                                Course = course,
                                Group = studentGroup,
                                Form = studentForm
                            };
                        }
                        else if (elemName == "Subject" && current != null && xmlReader.HasAttributes)
                        {
                            string subjName = "", grade = "", credits = "";
                            while (xmlReader.MoveToNextAttribute())
                            {
                                switch (xmlReader.Name)
                                {
                                    case "Name": subjName = xmlReader.Value; break;
                                    case "Grade": grade = xmlReader.Value; break;
                                    case "Credits": credits = xmlReader.Value; break;
                                }
                            }
                            current.Subjects.Add((subjName, grade, credits));
                        }
                        break;

                    case XmlNodeType.EndElement:
                        if (xmlReader.Name == "Student" && current != null)
                        {
                            bool subjectMatches = string.IsNullOrWhiteSpace(criteria.SubjectName) ||
                                                  current.Subjects.Any(s => s.Name.Equals(criteria.SubjectName, StringComparison.OrdinalIgnoreCase));

                            if (subjectMatches && XmlHelper.Matches(current.FullName, current.Faculty, current.Department, current.Course, current.Group, current.Form, criteria))
                            {
                                results.Add(current);
                            }
                            current = null;
                        }
                        break;
                }
            }
        }
        finally
        {
            xmlReader.Close();
        }

        return results;
    }

    public static string ParsingWithXmlTextReader(string xmlPath)
    {
        var sb = new StringBuilder();
        var xmlReader = new XmlTextReader(xmlPath);
        while (xmlReader.Read())
        {
            switch (xmlReader.NodeType)
            {
                case XmlNodeType.XmlDeclaration:
                case XmlNodeType.Element:
                case XmlNodeType.Comment:
                    sb.AppendFormat("{0}: {1} = {2}", xmlReader.NodeType, xmlReader.Name, xmlReader.Value);
                    sb.AppendLine();
                    break;
                case XmlNodeType.Text:
                    sb.AppendFormat(" - Value: {0}", xmlReader.Value);
                    sb.AppendLine();
                    break;
            }
            if (xmlReader.HasAttributes)
            {
                while (xmlReader.MoveToNextAttribute())
                {
                    sb.AppendFormat(" - Attribute: {0} = {1}", xmlReader.Name, xmlReader.Value);
                    sb.AppendLine();
                }
            }
        }
        xmlReader.Close();
        return sb.ToString();
    }
}

public class DomSearchStrategy : IXmlSearchStrategy
{
    public string StrategyName => "DOM API (XmlDocument + XPath)";

    public List<StudentRecord> Search(string xmlPath, SearchCriteria criteria)
    {
        var results = new List<StudentRecord>();
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlPath);

        string xpath = criteria.ToXPathQuery();
        var elmts = xmlDoc.SelectNodes(xpath);
        if (elmts == null) return results;

        foreach (XmlNode node in elmts)
        {
            string fullName = node.Attributes?["FullName"]?.Value ?? "";
            string faculty = node.Attributes?["Faculty"]?.Value ?? "";
            string department = node.Attributes?["Department"]?.Value ?? "";
            string course = node.Attributes?["Course"]?.Value ?? "";
            string studentGroup = node.Attributes?["Group"]?.Value ?? "Не вказано";
            string studentForm = node.Attributes?["Form"]?.Value ?? "Не вказано";

            if (!XmlHelper.Matches(fullName, faculty, department, course, studentGroup, studentForm, criteria))
                continue;

            var record = new StudentRecord
            {
                FullName = fullName,
                Faculty = faculty,
                Department = department,
                Course = course,
                Group = studentGroup,
                Form = studentForm
            };

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.Name == "Subject")
                {
                    record.Subjects.Add((
                        child.Attributes?["Name"]?.Value ?? "",
                        child.Attributes?["Grade"]?.Value ?? "",
                        child.Attributes?["Credits"]?.Value ?? ""
                    ));
                }
            }
            results.Add(record);
        }
        return results;
    }

    public static string ParseXmlTreeRecursively(string xmlPath)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlPath);
        var sb = new StringBuilder();
        if (xmlDoc.DocumentElement != null)
        {
            RecurseNodes(xmlDoc.DocumentElement, 0, sb);
        }
        return sb.ToString();
    }

    public static void RecurseNodes(XmlNode node, int level, StringBuilder sb)
    {
        sb.AppendFormat("{0,-2} Type:{1,-9} Name:{2,-18} Attr: ", level, node.NodeType, node.Name);
        if (node.Attributes != null)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                sb.AppendFormat("{0}={1} ", attr.Name, attr.Value);
            }
        }
        sb.AppendLine();
        foreach (XmlNode n in node.ChildNodes)
        {
            RecurseNodes(n, level + 1, sb);
        }
    }
}

public class LinqSearchStrategy : IXmlSearchStrategy
{
    public string StrategyName => "LINQ to XML (XDocument)";

    public List<StudentRecord> Search(string xmlPath, SearchCriteria criteria)
    {
        var doc = XDocument.Load(xmlPath);

        var result = (from student in doc.Descendants("Student")
                      let fullName = (string?)student.Attribute("FullName") ?? ""
                      let faculty = (string?)student.Attribute("Faculty") ?? ""
                      let department = (string?)student.Attribute("Department") ?? ""
                      let course = (string?)student.Attribute("Course") ?? ""
                      let studentGroup = (string?)student.Attribute("Group") ?? "Не вказано"
                      let studentForm = (string?)student.Attribute("Form") ?? "Не вказано"
                      where (string.IsNullOrWhiteSpace(criteria.SubjectName) ||
                             student.Descendants("Subject").Attributes("Name").Any(z => z.Value == criteria.SubjectName))
                            && XmlHelper.Matches(fullName, faculty, department, course, studentGroup, studentForm, criteria)
                      select new StudentRecord
                      {
                          FullName = fullName,
                          Faculty = faculty,
                          Department = department,
                          Course = course,
                          Group = studentGroup,
                          Form = studentForm,
                          Subjects = student.Elements("Subject")
                              .Select(s => (
                                  (string?)s.Attribute("Name") ?? "",
                                  (string?)s.Attribute("Grade") ?? "",
                                  (string?)s.Attribute("Credits") ?? ""
                              )).ToList()
                      }).ToList();

        return result;
    }
}

public class XmlAnalyzerContext
{
    private IXmlSearchStrategy _strategy;

    public XmlAnalyzerContext(IXmlSearchStrategy strategy) => _strategy = strategy;

    public void SetStrategy(IXmlSearchStrategy strategy) => _strategy = strategy;

    public List<StudentRecord> ExecuteSearch(string xmlPath, SearchCriteria criteria) =>
        _strategy.Search(xmlPath, criteria);

    public void TransformToHtml(string xmlPath, string xslPath, string outputHtmlPath)
    {
        XslCompiledTransform xslt = new XslCompiledTransform();
        xslt.Load(xslPath);
        xslt.Transform(xmlPath, outputHtmlPath);
    }

    public void TransformXmlToXml(string xmlPath, string xslXmlPath, string outputXmlPath)
    {
        XslCompiledTransform xslt = new XslCompiledTransform();
        xslt.Load(xslXmlPath);
        xslt.Transform(xmlPath, outputXmlPath);
    }
}

public static class PracticeTasksService
{
    public static string ExecuteLinq2And3()
    {
        int[] numbers = { 5, 4, 1, 3, 9, 8, 6, 7, 2, 0 };

        var greaterThanFive = from n in numbers where n > 5 select n;
        var lowNums = from n in numbers where n < 5 select n;
        var numsPlusOne = from n in numbers select n + 1;

        var sb = new StringBuilder();
        sb.AppendLine("Завдання 2 (LINQ to Object, елементи > 5):");
        foreach (var x in greaterThanFive) sb.AppendLine(x.ToString());

        sb.AppendLine("\nЗавдання 2 (Numbers < 5):");
        foreach (var x in lowNums) sb.AppendLine(x.ToString());

        sb.AppendLine("\nЗавдання 3 (Numbers + 1):");
        foreach (var i in numsPlusOne) sb.AppendLine(i.ToString());

        return sb.ToString();
    }

    public static string CreateMyRootXml()
    {
        string path = Path.Combine(XmlHelper.GetWorkFolder(), "MyRoot.xml");
        var xmlDoc = new XmlDocument();
        xmlDoc.AppendChild(xmlDoc.CreateXmlDeclaration("1.0", "utf-8", null));
        XmlElement root = xmlDoc.CreateElement("MyRoot");
        xmlDoc.AppendChild(root);

        for (int childCounter = 1; childCounter <= 3; childCounter++)
        {
            XmlElement childElmt = xmlDoc.CreateElement("MyChild");
            XmlAttribute childAttr = xmlDoc.CreateAttribute("ID");
            childAttr.Value = childCounter.ToString();
            childElmt.Attributes.Append(childAttr);
            root.AppendChild(childElmt);

            for (int grandChildCounter = 1; grandChildCounter <= 2; grandChildCounter++)
            {
                XmlElement grandChild = xmlDoc.CreateElement("MyGrandChild");
                XmlAttribute grandChildAttr = xmlDoc.CreateAttribute("NAME");
                grandChildAttr.Value = childAttr.Value + " " + grandChildCounter.ToString();
                grandChild.Attributes.Append(grandChildAttr);
                childElmt.AppendChild(grandChild);
            }
        }

        xmlDoc.Save(path);
        return path;
    }

    public static string SearchMyRootById(string id, bool useSax, bool useDom)
    {
        string path = CreateMyRootXml();
        var sb = new StringBuilder();
        sb.AppendLine($"Створений документ MyRoot.xml (Завдання 4):\n{File.ReadAllText(path)}\n");
        sb.AppendLine(new string('-', 70));

        if (useDom)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.Load(path);
            var node = xmlDoc.SelectSingleNode($"//MyChild[@ID='{id}']");
            sb.AppendLine($"Завдання 4 (DOM SelectSingleNode за ID='{id}'):");
            if (node != null) DomSearchStrategy.RecurseNodes(node, 0, sb);
            else sb.AppendLine("Елемент не знайдено.");
        }
        else if (useSax)
        {
            sb.AppendLine($"Завдання 5 (SAX XmlTextReader за ID='{id}'):");
            var reader = new XmlTextReader(path);
            bool insideTarget = false;
            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.Name == "MyChild")
                {
                    string? currentId = reader.GetAttribute("ID");
                    insideTarget = currentId == id;
                    if (insideTarget)
                        sb.AppendLine($"Element: MyChild | Attribute: ID={currentId}");
                }
                else if (reader.NodeType == XmlNodeType.Element && reader.Name == "MyGrandChild" && insideTarget)
                {
                    sb.AppendLine($"  Element: MyGrandChild | Attribute: NAME={reader.GetAttribute("NAME")}");
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "MyChild" && insideTarget)
                {
                    insideTarget = false;
                }
            }
            reader.Close();
        }
        else
        {
            sb.AppendLine($"Завдання 6 (LINQ to XML за ID='{id}'):");
            var xdoc = XDocument.Load(path);
            var items = from c in xdoc.Descendants("MyChild")
                        where (string?)c.Attribute("ID") == id
                        select c;
            foreach (var item in items)
                sb.AppendLine(item.ToString());
        }

        return sb.ToString();
    }

    public static string TransformMyRootToHtml()
    {
        string xmlPath = CreateMyRootXml();
        string xslPath = Path.Combine(XmlHelper.GetWorkFolder(), "MyRoot.xsl");
        string htmlPath = Path.Combine(XmlHelper.GetWorkFolder(), "MyRoot.html");

        string[] xslLines =
        {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" version=\"1.0\">",
            "  <xsl:output method=\"html\" encoding=\"UTF-8\" indent=\"yes\"/>",
            "  <xsl:template match=\"/MyRoot\">",
            "    <html>",
            "      <head><meta charset=\"UTF-8\"/><title>Завдання 7 — MyRoot HTML</title></head>",
            "      <body>",
            "        <h2>Таблиця MyRoot (Завдання 7)</h2>",
            "        <table border=\"2\" cellpadding=\"6\" cellspacing=\"0\">",
            "          <tr><th>MyChild ID</th><th>Вкладені елементи MyGrandChild (NAME)</th></tr>",
            "          <xsl:for-each select=\"MyChild\">",
            "            <tr>",
            "              <td><b><xsl:value-of select=\"@ID\"/></b></td>",
            "              <td>",
            "                <xsl:for-each select=\"MyGrandChild\">",
            "                  <div><xsl:value-of select=\"@NAME\"/></div>",
            "                </xsl:for-each>",
            "              </td>",
            "            </tr>",
            "          </xsl:for-each>",
            "        </table>",
            "      </body>",
            "    </html>",
            "  </xsl:template>",
            "</xsl:stylesheet>"
        };
        File.WriteAllLines(xslPath, xslLines, Encoding.UTF8);

        var xslt = new XslCompiledTransform();
        xslt.Load(xslPath);
        xslt.Transform(xmlPath, htmlPath);
        return htmlPath;
    }
}

public sealed class EventLogger
{
    private static readonly Lazy<EventLogger> _instance = new(() => new EventLogger());
    private readonly string _logFilePath;

    private EventLogger()
    {
        string folder = XmlHelper.GetWorkFolder();
        _logFilePath = Path.Combine(folder, "protocol.log");
    }

    public static EventLogger Instance => _instance.Value;
    public string LogFilePath => _logFilePath;

    public void Log(string category, string message)
    {
        string line = $"{DateTime.Now:dd.MM.yyyy HH:mm:ss} {category}. {message}{Environment.NewLine}";
        File.AppendAllText(_logFilePath, line, Encoding.UTF8);
    }
}

public abstract class FilteredFileSaver
{
    public abstract void Save(string filePath, List<StudentRecord> records);
}

public class XmlFilteredSaver : FilteredFileSaver
{
    public override void Save(string filePath, List<StudentRecord> records)
    {
        var doc = new XDocument(
            new XElement("UniversityDatabase",
                records.Select(r => new XElement("Student",
                    new XAttribute("FullName", r.FullName),
                    new XAttribute("Faculty", r.Faculty),
                    new XAttribute("Department", r.Department),
                    new XAttribute("Course", r.Course),
                    new XAttribute("Group", r.Group),
                    r.Subjects.Select(s => new XElement("Subject",
                        new XAttribute("Name", s.Name),
                        new XAttribute("Grade", s.Grade),
                        new XAttribute("Credits", s.Credits)
                    ))
                ))
            )
        );
        doc.Save(filePath);
    }
}

public class HtmlFilteredSaver : FilteredFileSaver
{
    public override void Save(string filePath, List<StudentRecord> records)
    {
        string tempXml = Path.Combine(XmlHelper.GetWorkFolder(), "temp_filtered.xml");
        string xslPath = Path.Combine(XmlHelper.GetWorkFolder(), "output.xsl");
        new XmlFilteredSaver().Save(tempXml, records);

        XslCompiledTransform xslt = new XslCompiledTransform();
        xslt.Load(xslPath);
        xslt.Transform(tempXml, filePath);
    }
}

public abstract class SaverCreator
{
    public abstract FilteredFileSaver CreateSaver();
}

public class XmlSaverCreator : SaverCreator
{
    public override FilteredFileSaver CreateSaver() => new XmlFilteredSaver();
}

public class HtmlSaverCreator : SaverCreator
{
    public override FilteredFileSaver CreateSaver() => new HtmlFilteredSaver();
}

public static class XmlHelper
{
    public static string GetWorkFolder()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string folder = Path.Combine(desktop, "Lab2_XML_Files");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static (string xmlPath, string xslHtmlPath, string xslXmlPath) EnsureSampleFilesCreated()
    {
        string folder = GetWorkFolder();
        string xmlPath = Path.Combine(folder, "StudentsSuccess.xml");
        string xslHtmlPath = Path.Combine(folder, "output.xsl");
        string xslXmlPath = Path.Combine(folder, "xml_xml.xsl");

        if (!File.Exists(xmlPath))
        {
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", "yes"),
                new XElement("UniversityDatabase",
                    new XElement("Student",
                        new XAttribute("FullName", "Джалялов Назар"),
                        new XAttribute("Faculty", "ФІТ"),
                        new XAttribute("Department", "ІПЗ"),
                        new XAttribute("Course", "2"),
                        new XAttribute("Group", "К-16"),
                        new XAttribute("Form", "Денна"),
                        new XElement("Subject",
                            new XAttribute("Name", "Об'єктно-орієнтоване програмування"),
                            new XAttribute("Grade", "95"),
                            new XAttribute("Credits", "5")),
                        new XElement("Subject",
                            new XAttribute("Name", "Теорія ймовірностей"),
                            new XAttribute("Grade", "92"),
                            new XAttribute("Credits", "4"))
                    ),
                    new XElement("Student",
                        new XAttribute("FullName", "Коваленко Олександр"),
                        new XAttribute("Faculty", "ФІТ"),
                        new XAttribute("Department", "Кібербезпека"),
                        new XAttribute("Course", "2"),
                        new XAttribute("Group", "К-14"),
                        new XElement("Subject",
                            new XAttribute("Name", "Об'єктно-орієнтоване програмування"),
                            new XAttribute("Grade", "88"),
                            new XAttribute("Credits", "5")),
                        new XElement("Subject",
                            new XAttribute("Name", "Комп'ютерні мережі"),
                            new XAttribute("Grade", "90"),
                            new XAttribute("Credits", "4"))
                    ),
                    new XElement("Student",
                        new XAttribute("FullName", "Мельник Софія"),
                        new XAttribute("Faculty", "ФКНК"),
                        new XAttribute("Department", "Системний аналіз"),
                        new XAttribute("Course", "3"),
                        new XAttribute("Form", "Денна"),
                        new XElement("Subject",
                            new XAttribute("Name", "Методи оптимізації"),
                            new XAttribute("Grade", "96"),
                            new XAttribute("Credits", "5")),
                        new XElement("Subject",
                            new XAttribute("Name", "Бази даних"),
                            new XAttribute("Grade", "94"),
                            new XAttribute("Credits", "4"))
                    ),
                    new XElement("Student",
                        new XAttribute("FullName", "Бондаренко Максим"),
                        new XAttribute("Faculty", "ФІТ"),
                        new XAttribute("Department", "ІПЗ"),
                        new XAttribute("Course", "1"),
                        new XElement("Subject",
                            new XAttribute("Name", "Програмування C++"),
                            new XAttribute("Grade", "85"),
                            new XAttribute("Credits", "6")),
                        new XElement("Subject",
                            new XAttribute("Name", "Дискретна математика"),
                            new XAttribute("Grade", "82"),
                            new XAttribute("Credits", "5"))
                    )
                )
            );
            doc.Save(xmlPath);
        }

        string[] htmlXslLines =
        {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" version=\"1.0\">",
            "  <xsl:output method=\"html\" encoding=\"UTF-8\" indent=\"yes\"/>",
            "  <xsl:template match=\"UniversityDatabase\">",
            "    <HTML>",
            "      <HEAD><meta charset=\"UTF-8\"/><TITLE>Список студентів</TITLE></HEAD>",
            "      <BODY>",
            "        <p><H2>Список успішності студентів</H2></p>",
            "        <TABLE BORDER=\"2\" CELLPADDING=\"6\" CELLSPACING=\"0\">",
            "          <TR>",
            "            <TD><b>П.І.П. студента</b></TD>",
            "            <TD><b>Факультет</b></TD>",
            "            <TD><b>Кафедра</b></TD>",
            "            <TD><b>Курс</b></TD>",
            "            <TD><b>Дисципліни (відсортовані за назвою)</b></TD>",
            "          </TR>",
            "          <xsl:apply-templates select=\"Student\"/>",
            "        </TABLE>",
            "      </BODY>",
            "    </HTML>",
            "  </xsl:template>",
            "  <xsl:template match=\"Student\">",
            "    <TR>",
            "      <TD><b><xsl:value-of select=\"@FullName\"/></b></TD>",
            "      <TD><xsl:value-of select=\"@Faculty\"/></TD>",
            "      <TD><xsl:value-of select=\"@Department\"/></TD>",
            "      <TD><xsl:value-of select=\"@Course\"/></TD>",
            "      <TD>",
            "        <xsl:for-each select=\"Subject\">",
            "          <xsl:sort select=\"@Name\"/>",
            "          <xsl:if test=\"@Grade &gt;= 60\">",
            "            <p><xsl:value-of select=\"@Name\"/>: <xsl:value-of select=\"@Grade\"/> балів (<xsl:value-of select=\"@Credits\"/> кр.)</p>",
            "          </xsl:if>",
            "        </xsl:for-each>",
            "      </TD>",
            "    </TR>",
            "  </xsl:template>",
            "</xsl:stylesheet>"
        };
        File.WriteAllLines(xslHtmlPath, htmlXslLines, Encoding.UTF8);

        string[] xmlToXmlLines =
        {
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>",
            "<xsl:stylesheet xmlns:xsl=\"http://www.w3.org/1999/XSL/Transform\" version=\"1.0\">",
            "  <xsl:output method=\"xml\" encoding=\"UTF-8\" indent=\"yes\"/>",
            "  <xsl:template match=\"UniversityDatabase\">",
            "    <transform>",
            "      <xsl:apply-templates select=\"Student\"/>",
            "    </transform>",
            "  </xsl:template>",
            "  <xsl:template match=\"Student\">",
            "    <student>",
            "      <FullName><xsl:value-of select=\"@FullName\"/></FullName>",
            "      <Faculty><xsl:value-of select=\"@Faculty\"/></Faculty>",
            "      <Department><xsl:value-of select=\"@Department\"/></Department>",
            "      <Course><xsl:value-of select=\"@Course\"/></Course>",
            "      <subjects>",
            "        <xsl:for-each select=\"Subject\">",
            "          <xsl:sort select=\"@Name\"/>",
            "          <xsl:value-of select=\"@Name\"/>",
            "          <xsl:if test=\"position() != last()\">",
            "            <xsl:text>, </xsl:text>",
            "          </xsl:if>",
            "        </xsl:for-each>",
            "      </subjects>",
            "    </student>",
            "  </xsl:template>",
            "</xsl:stylesheet>"
        };
        File.WriteAllLines(xslXmlPath, xmlToXmlLines, Encoding.UTF8);

        return (xmlPath, xslHtmlPath, xslXmlPath);
    }

    public static bool Matches(string fullName, string faculty, string department, string course, string studentGroup, string studentForm, SearchCriteria c)
    {
        bool matchName = string.IsNullOrWhiteSpace(c.FullName) || fullName.Equals(c.FullName, StringComparison.OrdinalIgnoreCase);
        bool matchFaculty = string.IsNullOrWhiteSpace(c.Faculty) || faculty.Equals(c.Faculty, StringComparison.OrdinalIgnoreCase);
        bool matchDept = string.IsNullOrWhiteSpace(c.Department) || department.Equals(c.Department, StringComparison.OrdinalIgnoreCase);
        bool matchCourse = string.IsNullOrWhiteSpace(c.Course) || course.Equals(c.Course, StringComparison.OrdinalIgnoreCase);

        bool matchKeyword = string.IsNullOrWhiteSpace(c.Keyword) ||
                            fullName.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase) ||
                            faculty.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase) ||
                            department.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase) ||
                            studentGroup.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase) ||
                            studentForm.Contains(c.Keyword, StringComparison.OrdinalIgnoreCase);

        return matchName && matchFaculty && matchDept && matchCourse && matchKeyword;
    }

    public static Dictionary<string, List<string>> ExtractAttributes(string xmlPath)
    {
        var dict = new Dictionary<string, List<string>>
        {
            ["FullName"] = new(),
            ["Faculty"] = new(),
            ["Department"] = new(),
            ["Course"] = new(),
            ["SubjectName"] = new()
        };

        XDocument doc = XDocument.Load(xmlPath);
        foreach (var el in doc.Descendants("Student"))
        {
            foreach (var key in new[] { "FullName", "Faculty", "Department", "Course" })
            {
                string? val = (string?)el.Attribute(key);
                if (!string.IsNullOrWhiteSpace(val) && !dict[key].Contains(val))
                    dict[key].Add(val);
            }
        }
        foreach (var subj in doc.Descendants("Subject"))
        {
            string? val = (string?)subj.Attribute("Name");
            if (!string.IsNullOrWhiteSpace(val) && !dict["SubjectName"].Contains(val))
                dict["SubjectName"].Add(val);
        }
        return dict;
    }
}