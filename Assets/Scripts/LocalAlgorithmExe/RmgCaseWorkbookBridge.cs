using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Vtp.VR.Integration;
using Vtp.VR.Rest;

namespace Vtp.VR.LocalAlgorithmExe
{
    /// <summary>
    /// 算例数据与独立算法EXE之间的Excel桥接层。
    ///
    /// EXE 0.2.0只支持从接口或固定列名的Excel读取任务，不能直接接收
    /// Unity中的JSON对象。因此每次运行前，把“查看算例”页面已经缓存并
    /// 按实验类型筛选的同一批数据写成本次专用Excel，再让EXE读取它。
    /// </summary>
    public static class RmgCaseWorkbookBridge
    {
        private const string WorksheetEntry =
            "xl/worksheets/sheet1.xml";
        private const string SharedStringsEntry =
            "xl/sharedStrings.xml";
        private const string LocalMetadataPrefix =
            "RMG-XLSX|";

        private static readonly string[] Headers =
        {
            "任务编号",
            "任务类型",
            "起点类型",
            "起点贝位",
            "起点列",
            "起点层",
            "堆场箱区号",
            "箱位号"
        };

        private sealed class WorkbookRow
        {
            public int TaskNo;
            public string TaskType;
            public string SourceType;
            public int SourceBay;
            public int SourceColumn;
            public int SourceTier;
            public string YardBlock;
            public string BoxPositionCode;
        }

        /// <summary>
        /// 读取ZIP随附的本地测试算例，并只返回当前实验类型。
        /// </summary>
        public static bool TryReadExamples(
            string workbookPath,
            string taskId,
            string requiredTaskType,
            out SceneNumericalExampleDto[] examples,
            out string error)
        {
            examples = new SceneNumericalExampleDto[0];
            error = null;

            bool filterByTaskType =
                !string.IsNullOrWhiteSpace(requiredTaskType);
            string normalizedRequiredType = null;
            if (filterByTaskType &&
                !TryNormalizeTaskType(
                    requiredTaskType,
                    out normalizedRequiredType))
            {
                error = "无法识别要筛选的作业类型：" + requiredTaskType;
                return false;
            }

            if (string.IsNullOrWhiteSpace(workbookPath) ||
                !File.Exists(workbookPath))
            {
                error = "本地测试算例文件不存在：" + workbookPath;
                return false;
            }

            try
            {
                using (ZipArchive archive =
                    ZipFile.OpenRead(workbookPath))
                {
                    List<string> sharedStrings =
                        ReadSharedStrings(archive);
                    List<string[]> rows =
                        ReadWorksheetRows(
                            archive,
                            sharedStrings);

                    if (rows.Count < 2)
                    {
                        error = "本地测试算例Excel没有数据行。";
                        return false;
                    }

                    Dictionary<string, int> columns =
                        BuildHeaderMap(rows[0]);
                    for (int index = 0;
                         index < Headers.Length;
                         index++)
                    {
                        if (!columns.ContainsKey(Headers[index]))
                        {
                            error =
                                "本地测试算例缺少列：" +
                                Headers[index];
                            return false;
                        }
                    }

                    List<SceneNumericalExampleDto> result =
                        new List<SceneNumericalExampleDto>();
                    string normalizedTaskId =
                        string.IsNullOrWhiteSpace(taskId)
                            ? "local-test"
                            : taskId.Trim();

                    for (int rowIndex = 1;
                         rowIndex < rows.Count;
                         rowIndex++)
                    {
                        string[] row = rows[rowIndex];
                        string taskType;
                        if (!TryNormalizeTaskType(
                                ReadColumn(row, columns, "任务类型"),
                                out taskType) ||
                            (filterByTaskType &&
                             !string.Equals(
                                taskType,
                                normalizedRequiredType,
                                StringComparison.Ordinal)))
                        {
                            continue;
                        }

                        int sourceBay = ReadIntColumn(
                            row,
                            columns,
                            "起点贝位",
                            0);
                        int sourceColumn = Math.Max(
                            1,
                            ReadIntColumn(
                                row,
                                columns,
                                "起点列",
                                1));
                        int sourceTier = Clamp(
                            ReadIntColumn(
                                row,
                                columns,
                                "起点层",
                                1),
                            1,
                            5);
                        string sourceType = ReadColumn(
                            row,
                            columns,
                            "起点类型");
                        string block = ReadColumn(
                            row,
                            columns,
                            "堆场箱区号");
                        string boxCode = ReadColumn(
                            row,
                            columns,
                            "箱位号");

                        int boxBay;
                        int boxColumn;
                        int boxTier;
                        ParseBoxPosition(
                            boxCode,
                            sourceBay,
                            sourceColumn,
                            sourceTier,
                            out boxBay,
                            out boxColumn,
                            out boxTier);

                        string no = ReadColumn(
                            row,
                            columns,
                            "任务编号");
                        if (string.IsNullOrWhiteSpace(no))
                            no = (result.Count + 1).ToString();

                        result.Add(
                            new SceneNumericalExampleDto
                            {
                                id = "ZIP-LOCAL-" + no,
                                courseTaskId = normalizedTaskId,
                                no = no,
                                // 暂存在原DTO的deptId中，仅供再次写入EXE输入表时
                                // 无损还原ZIP的起点类型、层、箱区和箱位号。
                                deptId = BuildLocalMetadata(
                                    sourceType,
                                    sourceTier,
                                    block,
                                    boxCode),
                                argm = "待算法分配",
                                type = taskType,
                                startPointPosition = sourceBay,
                                startList = sourceColumn,
                                endPointPosition = ResolveTargetBay(
                                    taskType,
                                    boxBay,
                                    rowIndex),
                                endList = boxColumn,
                                time_consuming = 0f
                            });
                    }

                    if (result.Count == 0)
                    {
                        error = filterByTaskType
                            ? "ZIP本地测试算例中没有“" +
                              normalizedRequiredType +
                              "”任务。"
                            : "ZIP本地测试算例中没有可识别的任务。";
                        return false;
                    }

                    examples = result.ToArray();
                    return true;
                }
            }
            catch (Exception exception)
            {
                error =
                    "本地测试算例Excel读取失败：" +
                    exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 把当前缓存的同一批算例写成本次算法运行专用Excel。
        /// </summary>
        public static bool TryWriteExamples(
            string workbookPath,
            SceneNumericalExampleDto[] examples,
            string requiredTaskType,
            out int writtenCount,
            out string error)
        {
            return TryWriteExamples(
                workbookPath,
                examples,
                requiredTaskType,
                null,
                out writtenCount,
                out error);
        }

        public static bool TryWriteExamples(
            string workbookPath,
            SceneNumericalExampleDto[] examples,
            string requiredTaskType,
            SceneNumericalExampleDto[] compatibilityExamples,
            out int writtenCount,
            out string error)
        {
            writtenCount = 0;
            error = null;

            string normalizedRequiredType;
            if (!TryNormalizeTaskType(
                    requiredTaskType,
                    out normalizedRequiredType))
            {
                error = "无法识别要执行的作业类型：" + requiredTaskType;
                return false;
            }

            if (examples == null || examples.Length == 0)
            {
                error = "当前没有可传入算法EXE的算例。";
                return false;
            }

            List<WorkbookRow> rows = new List<WorkbookRow>();
            for (int index = 0; index < examples.Length; index++)
            {
                SceneNumericalExampleDto example = examples[index];
                if (example == null)
                    continue;

                string taskType;
                if (!TryNormalizeTaskType(example.type, out taskType) ||
                    !string.Equals(
                        taskType,
                        normalizedRequiredType,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                rows.Add(
                    BuildWorkbookRow(
                        example,
                        taskType,
                        rows.Count + 1));
            }

            if (rows.Count == 0)
            {
                error =
                    "当前缓存中没有“" +
                    normalizedRequiredType +
                    "”算例，未启动算法EXE。";
                return false;
            }

            int userTaskCount = rows.Count;

            // 当前ZIP中的0.2.0算法要求输入中存在足够的翻箱任务，供
            // 六台RMG和跨箱区目标初始化。优先使用同一次服务器/ZIP
            // 原始响应中的翻箱行；最终结果仍会按requiredTaskType剔除。
            AppendCompatibilityExamples(
                rows,
                normalizedRequiredType,
                compatibilityExamples);

            if (!string.Equals(
                    normalizedRequiredType,
                    "翻箱",
                    StringComparison.Ordinal) &&
                !ContainsTaskType(rows, "翻箱"))
            {
                AppendExeCompatibilityRows(
                    rows,
                    normalizedRequiredType);
            }

            try
            {
                string directory = Path.GetDirectoryName(workbookPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(
                    workbookPath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None))
                using (ZipArchive archive = new ZipArchive(
                    stream,
                    ZipArchiveMode.Create,
                    false,
                    Encoding.UTF8))
                {
                    WritePackage(archive, rows);
                }

                writtenCount = userTaskCount;
                return true;
            }
            catch (Exception exception)
            {
                error =
                    "无法生成本次算法输入Excel：" +
                    exception.Message;
                return false;
            }
        }

        private static void WritePackage(
            ZipArchive archive,
            IList<WorkbookRow> rows)
        {
            XNamespace contentTypes =
                "http://schemas.openxmlformats.org/package/2006/content-types";
            WriteXmlEntry(
                archive,
                "[Content_Types].xml",
                new XDocument(
                    new XElement(
                        contentTypes + "Types",
                        new XElement(
                            contentTypes + "Default",
                            new XAttribute("Extension", "rels"),
                            new XAttribute(
                                "ContentType",
                                "application/vnd.openxmlformats-package.relationships+xml")),
                        new XElement(
                            contentTypes + "Default",
                            new XAttribute("Extension", "xml"),
                            new XAttribute(
                                "ContentType",
                                "application/xml")),
                        CreateOverride(
                            contentTypes,
                            "/xl/workbook.xml",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                        CreateOverride(
                            contentTypes,
                            "/xl/worksheets/sheet1.xml",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"),
                        CreateOverride(
                            contentTypes,
                            "/xl/styles.xml",
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));

            XNamespace packageRelationships =
                "http://schemas.openxmlformats.org/package/2006/relationships";
            WriteXmlEntry(
                archive,
                "_rels/.rels",
                new XDocument(
                    new XElement(
                        packageRelationships + "Relationships",
                        new XElement(
                            packageRelationships + "Relationship",
                            new XAttribute("Id", "rId1"),
                            new XAttribute(
                                "Type",
                                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                            new XAttribute(
                                "Target",
                                "xl/workbook.xml")))));

            XNamespace spreadsheet =
                "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace officeRelationships =
                "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            WriteXmlEntry(
                archive,
                "xl/workbook.xml",
                new XDocument(
                    new XElement(
                        spreadsheet + "workbook",
                        new XAttribute(
                            XNamespace.Xmlns + "r",
                            officeRelationships),
                        new XElement(
                            spreadsheet + "sheets",
                            new XElement(
                                spreadsheet + "sheet",
                                new XAttribute("name", "任务数据"),
                                new XAttribute("sheetId", "1"),
                                new XAttribute(
                                    officeRelationships + "id",
                                    "rId1"))))));

            WriteXmlEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                new XDocument(
                    new XElement(
                        packageRelationships + "Relationships",
                        new XElement(
                            packageRelationships + "Relationship",
                            new XAttribute("Id", "rId1"),
                            new XAttribute(
                                "Type",
                                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                            new XAttribute(
                                "Target",
                                "worksheets/sheet1.xml")),
                        new XElement(
                            packageRelationships + "Relationship",
                            new XAttribute("Id", "rId2"),
                            new XAttribute(
                                "Type",
                                "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"),
                            new XAttribute(
                                "Target",
                                "styles.xml")))));

            WriteXmlEntry(
                archive,
                "xl/styles.xml",
                CreateStylesDocument(spreadsheet));
            WriteXmlEntry(
                archive,
                WorksheetEntry,
                CreateWorksheetDocument(spreadsheet, rows));
        }

        private static XElement CreateOverride(
            XNamespace ns,
            string partName,
            string contentType)
        {
            return new XElement(
                ns + "Override",
                new XAttribute("PartName", partName),
                new XAttribute("ContentType", contentType));
        }

        private static XDocument CreateStylesDocument(
            XNamespace spreadsheet)
        {
            return new XDocument(
                new XElement(
                    spreadsheet + "styleSheet",
                    new XElement(
                        spreadsheet + "fonts",
                        new XAttribute("count", "1"),
                        new XElement(spreadsheet + "font")),
                    new XElement(
                        spreadsheet + "fills",
                        new XAttribute("count", "1"),
                        new XElement(
                            spreadsheet + "fill",
                            new XElement(
                                spreadsheet + "patternFill",
                                new XAttribute(
                                    "patternType",
                                    "none")))),
                    new XElement(
                        spreadsheet + "borders",
                        new XAttribute("count", "1"),
                        new XElement(spreadsheet + "border")),
                    new XElement(
                        spreadsheet + "cellStyleXfs",
                        new XAttribute("count", "1"),
                        new XElement(
                            spreadsheet + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"))),
                    new XElement(
                        spreadsheet + "cellXfs",
                        new XAttribute("count", "1"),
                        new XElement(
                            spreadsheet + "xf",
                            new XAttribute("numFmtId", "0"),
                            new XAttribute("fontId", "0"),
                            new XAttribute("fillId", "0"),
                            new XAttribute("borderId", "0"),
                            new XAttribute("xfId", "0"))),
                    new XElement(
                        spreadsheet + "cellStyles",
                        new XAttribute("count", "1"),
                        new XElement(
                            spreadsheet + "cellStyle",
                            new XAttribute("name", "Normal"),
                            new XAttribute("xfId", "0"),
                            new XAttribute("builtinId", "0")))));
        }

        private static XDocument CreateWorksheetDocument(
            XNamespace spreadsheet,
            IList<WorkbookRow> rows)
        {
            XElement sheetData = new XElement(
                spreadsheet + "sheetData");
            XElement headerRow = new XElement(
                spreadsheet + "row",
                new XAttribute("r", "1"));
            for (int column = 0;
                 column < Headers.Length;
                 column++)
            {
                headerRow.Add(
                    CreateStringCell(
                        spreadsheet,
                        CellReference(column, 1),
                        Headers[column]));
            }
            sheetData.Add(headerRow);

            for (int index = 0; index < rows.Count; index++)
            {
                WorkbookRow item = rows[index];
                int rowNumber = index + 2;
                XElement row = new XElement(
                    spreadsheet + "row",
                    new XAttribute("r", rowNumber));
                row.Add(CreateNumberCell(
                    spreadsheet,
                    CellReference(0, rowNumber),
                    item.TaskNo));
                row.Add(CreateStringCell(
                    spreadsheet,
                    CellReference(1, rowNumber),
                    item.TaskType));
                row.Add(CreateStringCell(
                    spreadsheet,
                    CellReference(2, rowNumber),
                    item.SourceType));
                row.Add(CreateNumberCell(
                    spreadsheet,
                    CellReference(3, rowNumber),
                    item.SourceBay));
                row.Add(CreateNumberCell(
                    spreadsheet,
                    CellReference(4, rowNumber),
                    item.SourceColumn));
                row.Add(CreateNumberCell(
                    spreadsheet,
                    CellReference(5, rowNumber),
                    item.SourceTier));
                row.Add(CreateStringCell(
                    spreadsheet,
                    CellReference(6, rowNumber),
                    item.YardBlock));
                row.Add(CreateStringCell(
                    spreadsheet,
                    CellReference(7, rowNumber),
                    item.BoxPositionCode));
                sheetData.Add(row);
            }

            return new XDocument(
                new XElement(
                    spreadsheet + "worksheet",
                    new XElement(
                        spreadsheet + "dimension",
                        new XAttribute(
                            "ref",
                            "A1:H" + (rows.Count + 1))),
                    new XElement(
                        spreadsheet + "sheetViews",
                        new XElement(
                            spreadsheet + "sheetView",
                            new XAttribute(
                                "workbookViewId",
                                "0"))),
                    new XElement(
                        spreadsheet + "sheetFormatPr",
                        new XAttribute(
                            "defaultRowHeight",
                            "15")),
                    sheetData));
        }

        private static XElement CreateStringCell(
            XNamespace spreadsheet,
            string reference,
            string value)
        {
            return new XElement(
                spreadsheet + "c",
                new XAttribute("r", reference),
                new XAttribute("t", "inlineStr"),
                new XElement(
                    spreadsheet + "is",
                    new XElement(
                        spreadsheet + "t",
                        value ?? string.Empty)));
        }

        private static XElement CreateNumberCell(
            XNamespace spreadsheet,
            string reference,
            int value)
        {
            return new XElement(
                spreadsheet + "c",
                new XAttribute("r", reference),
                new XElement(
                    spreadsheet + "v",
                    value.ToString(CultureInfo.InvariantCulture)));
        }

        private static string CellReference(
            int zeroBasedColumn,
            int row)
        {
            return ((char)('A' + zeroBasedColumn)).ToString() + row;
        }

        private static void WriteXmlEntry(
            ZipArchive archive,
            string entryName,
            XDocument document)
        {
            ZipArchiveEntry entry = archive.CreateEntry(
                entryName,
                CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (XmlWriter writer = XmlWriter.Create(
                stream,
                new XmlWriterSettings
                {
                    Encoding = new UTF8Encoding(false),
                    Indent = false,
                    CloseOutput = false
                }))
            {
                document.Save(writer);
            }
        }

        private static List<string> ReadSharedStrings(
            ZipArchive archive)
        {
            List<string> result = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry(
                SharedStringsEntry);
            if (entry == null)
                return result;

            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                XNamespace spreadsheet =
                    "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                foreach (XElement item in
                    document.Descendants(spreadsheet + "si"))
                {
                    result.Add(
                        string.Concat(
                            item.Descendants(spreadsheet + "t")
                                .Select(value => value.Value)));
                }
            }

            return result;
        }

        private static List<string[]> ReadWorksheetRows(
            ZipArchive archive,
            IList<string> sharedStrings)
        {
            ZipArchiveEntry entry = archive.GetEntry(WorksheetEntry);
            if (entry == null)
                throw new InvalidDataException(
                    "Excel中找不到任务数据工作表。");

            List<string[]> result = new List<string[]>();
            using (Stream stream = entry.Open())
            {
                XDocument document = XDocument.Load(stream);
                XNamespace spreadsheet =
                    "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

                foreach (XElement row in
                    document.Descendants(spreadsheet + "row"))
                {
                    string[] values = new string[Headers.Length];
                    foreach (XElement cell in
                        row.Elements(spreadsheet + "c"))
                    {
                        int column = GetColumnIndex(
                            (string)cell.Attribute("r"));
                        if (column < 0 || column >= values.Length)
                            continue;

                        string cellType =
                            (string)cell.Attribute("t") ??
                            string.Empty;
                        if (cellType == "s")
                        {
                            int sharedIndex;
                            if (int.TryParse(
                                    (string)cell.Element(
                                        spreadsheet + "v"),
                                    out sharedIndex) &&
                                sharedIndex >= 0 &&
                                sharedIndex < sharedStrings.Count)
                            {
                                values[column] =
                                    sharedStrings[sharedIndex];
                            }
                        }
                        else if (cellType == "inlineStr")
                        {
                            values[column] = string.Concat(
                                cell.Descendants(spreadsheet + "t")
                                    .Select(value => value.Value));
                        }
                        else
                        {
                            XElement value = cell.Element(
                                spreadsheet + "v");
                            values[column] = value == null
                                ? string.Empty
                                : value.Value;
                        }
                    }

                    result.Add(values);
                }
            }

            return result;
        }

        private static int GetColumnIndex(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                return -1;

            int value = 0;
            int letterCount = 0;
            for (int index = 0;
                 index < reference.Length;
                 index++)
            {
                char character = char.ToUpperInvariant(
                    reference[index]);
                if (character < 'A' || character > 'Z')
                    break;
                value = value * 26 + character - 'A' + 1;
                letterCount++;
            }

            return letterCount == 0 ? -1 : value - 1;
        }

        private static Dictionary<string, int> BuildHeaderMap(
            string[] headerRow)
        {
            Dictionary<string, int> result =
                new Dictionary<string, int>(
                    StringComparer.OrdinalIgnoreCase);
            for (int index = 0;
                 index < headerRow.Length;
                 index++)
            {
                string value = headerRow[index];
                if (!string.IsNullOrWhiteSpace(value) &&
                    !result.ContainsKey(value.Trim()))
                {
                    result.Add(value.Trim(), index);
                }
            }
            return result;
        }

        private static string ReadColumn(
            string[] row,
            IDictionary<string, int> columns,
            string name)
        {
            int index;
            if (row == null ||
                !columns.TryGetValue(name, out index) ||
                index < 0 ||
                index >= row.Length)
            {
                return string.Empty;
            }
            return (row[index] ?? string.Empty).Trim();
        }

        private static int ReadIntColumn(
            string[] row,
            IDictionary<string, int> columns,
            string name,
            int fallback)
        {
            string value = ReadColumn(row, columns, name);
            int integer;
            if (int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out integer))
            {
                return integer;
            }

            double number;
            return double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out number)
                ? (int)Math.Round(number)
                : fallback;
        }

        private static bool TryNormalizeTaskType(
            string raw,
            out string taskType)
        {
            return SceneExampleTaskCaseAdapter.TryNormalizeTaskType(
                raw,
                out taskType);
        }

        private static string GetSourceType(string taskType)
        {
            switch (taskType)
            {
                case "卸船": return "海侧支架";
                case "卸车": return "陆侧车道";
                default: return "堆场";
            }
        }

        private static int ResolveTargetBay(
            string taskType,
            int yardBay,
            int rowIndex)
        {
            switch (taskType)
            {
                case "卸船":
                case "卸车":
                    // 本地ZIP只记录进口任务的边界起点，没有记录算法
                    // 后续分配的堆场目标。补一个稳定的有效堆场贝位，
                    // 使题面和场景DTO仍能表达完整的起止位置语义。
                    return yardBay > 0 && yardBay < 51
                        ? yardBay
                        : 1 + (Math.Max(1, rowIndex) - 1) % 50;
                case "装船": return 0;
                case "装车": return 51;
                default: return yardBay;
            }
        }

        private static WorkbookRow BuildWorkbookRow(
            SceneNumericalExampleDto example,
            string taskType,
            int taskNo)
        {
            string metadataSourceType;
            int metadataTier;
            string metadataBlock;
            string metadataBoxCode;
            bool hasMetadata = TryReadLocalMetadata(
                example.deptId,
                out metadataSourceType,
                out metadataTier,
                out metadataBlock,
                out metadataBoxCode);

            int sourceBay = Math.Max(
                0,
                RoundToInt(example.startPointPosition));
            int sourceColumn = Clamp(
                RoundToInt(example.startList),
                1,
                10);
            int sourceTier = hasMetadata
                ? Clamp(metadataTier, 1, 5)
                : 1;
            string block = hasMetadata
                ? NormalizeBlock(metadataBlock, "A40")
                : ResolveServerBlock(
                    example.deptId,
                    taskNo);
            string sourceType = hasMetadata &&
                                !string.IsNullOrWhiteSpace(
                                    metadataSourceType)
                ? metadataSourceType.Trim()
                : GetSourceType(taskType);
            string boxCode = hasMetadata &&
                             !string.IsNullOrWhiteSpace(
                                 metadataBoxCode)
                ? metadataBoxCode.Trim().ToUpperInvariant()
                : BuildBoxPositionCode(
                    block,
                    sourceBay,
                    sourceColumn,
                    sourceTier);

            return new WorkbookRow
            {
                TaskNo = taskNo,
                TaskType = taskType,
                SourceType = sourceType,
                SourceBay = sourceBay,
                SourceColumn = sourceColumn,
                SourceTier = sourceTier,
                YardBlock = block,
                BoxPositionCode = boxCode
            };
        }

        private static void AppendCompatibilityExamples(
            IList<WorkbookRow> rows,
            string requiredTaskType,
            SceneNumericalExampleDto[] compatibilityExamples)
        {
            if (string.Equals(
                    requiredTaskType,
                    "翻箱",
                    StringComparison.Ordinal) ||
                compatibilityExamples == null)
            {
                return;
            }

            for (int index = 0;
                 index < compatibilityExamples.Length;
                 index++)
            {
                SceneNumericalExampleDto example =
                    compatibilityExamples[index];
                string taskType;
                if (example != null &&
                    TryNormalizeTaskType(example.type, out taskType) &&
                    string.Equals(
                        taskType,
                        "翻箱",
                        StringComparison.Ordinal))
                {
                    rows.Add(
                        BuildWorkbookRow(
                            example,
                            taskType,
                            rows.Count + 1));
                }
            }
        }

        private static bool ContainsTaskType(
            IList<WorkbookRow> rows,
            string taskType)
        {
            for (int index = 0; index < rows.Count; index++)
            {
                if (string.Equals(
                        rows[index].TaskType,
                        taskType,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AppendExeCompatibilityRows(
            IList<WorkbookRow> rows,
            string requiredTaskType)
        {
            if (string.Equals(
                    requiredTaskType,
                    "翻箱",
                    StringComparison.Ordinal))
            {
                return;
            }

            string[] helperBlocks = { "A35", "A36", "A37" };
            const int helpersPerBlock = 20;
            for (int blockIndex = 0;
                 blockIndex < helperBlocks.Length;
                 blockIndex++)
            {
                string block = helperBlocks[blockIndex];
                for (int index = 0;
                     index < helpersPerBlock;
                     index++)
                {
                    int bay = 1 + index % 10 * 2;
                    int column = 1 + index / 10;
                    rows.Add(
                        new WorkbookRow
                        {
                            TaskNo = rows.Count + 1,
                            TaskType = "翻箱",
                            SourceType = "堆场",
                            SourceBay = bay,
                            SourceColumn = column,
                            SourceTier = 1,
                            YardBlock = block,
                            BoxPositionCode = BuildBoxPositionCode(
                                block,
                                bay,
                                column,
                                1)
                        });
                }
            }
        }

        private static string ResolveServerBlock(
            string departmentId,
            int taskNo)
        {
            string value =
                (departmentId ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
            if (value.Length == 3 &&
                value[0] == 'A' &&
                char.IsDigit(value[1]) &&
                char.IsDigit(value[2]))
            {
                return value;
            }

            // 服务器DTO没有箱区字段。独立EXE固定按每个箱区两台RMG
            // 建模，因此把任务稳定分配到其默认三箱区，避免少量任务
            // 因只出现一个箱区而被EXE判定为“RMG未承担任务”。
            string[] defaultBlocks = { "A35", "A36", "A37" };
            int index = Math.Max(0, taskNo - 1) % defaultBlocks.Length;
            return defaultBlocks[index];
        }

        private static string NormalizeBlock(
            string value,
            string fallback)
        {
            string result =
                (value ?? string.Empty)
                .Trim()
                .ToUpperInvariant();
            return string.IsNullOrWhiteSpace(result)
                ? fallback
                : result;
        }

        private static string BuildBoxPositionCode(
            string block,
            int bay,
            int column,
            int tier)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}{1:000}{2:00}{3:0}",
                NormalizeBlock(block, "A40"),
                Clamp(bay, 0, 999),
                Clamp(column, 1, 99),
                Clamp(tier, 1, 9));
        }

        private static void ParseBoxPosition(
            string code,
            int fallbackBay,
            int fallbackColumn,
            int fallbackTier,
            out int bay,
            out int column,
            out int tier)
        {
            bay = fallbackBay;
            column = fallbackColumn;
            tier = fallbackTier;

            string value = (code ?? string.Empty).Trim();
            if (value.Length < 9)
                return;

            int parsedBay;
            int parsedColumn;
            int parsedTier;
            if (int.TryParse(value.Substring(3, 3), out parsedBay) &&
                int.TryParse(value.Substring(6, 2), out parsedColumn) &&
                int.TryParse(value.Substring(8, 1), out parsedTier))
            {
                bay = parsedBay;
                column = parsedColumn;
                tier = parsedTier;
            }
        }

        private static string BuildLocalMetadata(
            string sourceType,
            int tier,
            string block,
            string boxCode)
        {
            return LocalMetadataPrefix +
                   SanitizeMetadataPart(sourceType) + "|" +
                   Clamp(tier, 1, 5) + "|" +
                   SanitizeMetadataPart(block) + "|" +
                   SanitizeMetadataPart(boxCode);
        }

        private static bool TryReadLocalMetadata(
            string value,
            out string sourceType,
            out int tier,
            out string block,
            out string boxCode)
        {
            sourceType = null;
            tier = 1;
            block = null;
            boxCode = null;

            if (string.IsNullOrWhiteSpace(value) ||
                !value.StartsWith(
                    LocalMetadataPrefix,
                    StringComparison.Ordinal))
            {
                return false;
            }

            string[] parts = value.Split('|');
            if (parts.Length != 5)
                return false;

            sourceType = parts[1];
            int parsedTier;
            if (int.TryParse(parts[2], out parsedTier))
                tier = parsedTier;
            block = parts[3];
            boxCode = parts[4];
            return true;
        }

        private static string SanitizeMetadataPart(string value)
        {
            return (value ?? string.Empty).Replace("|", string.Empty);
        }

        private static int RoundToInt(float value)
        {
            return (int)Math.Round(
                value,
                MidpointRounding.AwayFromZero);
        }

        private static int Clamp(
            int value,
            int minimum,
            int maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }
    }
}
