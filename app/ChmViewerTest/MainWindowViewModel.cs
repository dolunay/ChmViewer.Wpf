using HtmlHelp;
using HtmlHelp.ChmDecoding;
using Microsoft.Win32;
using System.Data;

namespace ChmViewerTest
{
    public class MainWindowViewModel
    {
        private readonly string LM_Key = @"Software\ChmViewerTest\Help";
        readonly HtmlHelpSystem _reader = null;
        DumpingInfo _dmpInfo = null;

        string _prefDumpOutput;

        DumpCompression _prefDumpCompression = DumpCompression.Medium;

        DumpingFlags _prefDumpFlags = DumpingFlags.DumpBinaryTOC | DumpingFlags.DumpTextTOC |
            DumpingFlags.DumpTextIndex | DumpingFlags.DumpBinaryIndex |
            DumpingFlags.DumpUrlStr | DumpingFlags.DumpStrings;

        string _prefURLPrefix = "mk:@MSITStore:";
        bool _prefUseHH2TreePics = false;

        public MainWindowViewModel(string fileName)
        {
            _reader = new HtmlHelpSystem();
            HtmlHelpSystem.UrlPrefix = "mk:@MSITStore:";

            // use temporary folder for data dumping
            string sTemp = System.Environment.GetEnvironmentVariable("TEMP");
            if (sTemp.Length <= 0)
                sTemp = System.Environment.GetEnvironmentVariable("TMP");

            _prefDumpOutput = sTemp;

            // create a dump info instance used for dumping data
            _dmpInfo =
                new DumpingInfo(DumpingFlags.DumpBinaryTOC | DumpingFlags.DumpTextTOC |
                DumpingFlags.DumpTextIndex | DumpingFlags.DumpBinaryIndex |
                DumpingFlags.DumpUrlStr | DumpingFlags.DumpStrings,
                sTemp, DumpCompression.Medium);

            LoadRegistryPreferences();

            HtmlHelpSystem.UrlPrefix = _prefURLPrefix;
            HtmlHelpSystem.UseHH2TreePics = _prefUseHH2TreePics;
            OpenFile(fileName);
        }

        private void LoadRegistryPreferences()
        {
            RegistryKey regKey = Registry.CurrentUser.CreateSubKey(LM_Key);

            bool bEnable = bool.Parse(regKey.GetValue("EnableDumping", true).ToString());

            _prefDumpOutput = (string)regKey.GetValue("DumpOutputDir", _prefDumpOutput);
            _prefDumpCompression = (DumpCompression)((int)regKey.GetValue("CompressionLevel", _prefDumpCompression));
            _prefDumpFlags = (DumpingFlags)((int)regKey.GetValue("DumpingFlags", _prefDumpFlags));

            if (bEnable)
                _dmpInfo = new DumpingInfo(_prefDumpFlags, _prefDumpOutput, _prefDumpCompression);
            else
                _dmpInfo = null;

            _prefURLPrefix = (string)regKey.GetValue("ITSUrlPrefix", _prefURLPrefix);
            _prefUseHH2TreePics = bool.Parse(regKey.GetValue("UseHH2TreePics", _prefUseHH2TreePics).ToString());
        }

        private void OpenFile(string fileName)
        {
            _reader.OpenFile(fileName, _dmpInfo);
        }

        public DataTable Search(string keyword)
        {
            return _reader.PerformSearch(keyword, 500, true, true);
        }
    }
}
