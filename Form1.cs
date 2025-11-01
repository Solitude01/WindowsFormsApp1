using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;

// 命名空间必须与您的项目名一致
namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        private System.Timers.Timer _fileTimer;
        private bool _isExiting = false;
        private static readonly object _logLock = new object();
        private string _modernFolderDialogLastPath = "";

        public Form1()
        {
            InitializeComponent();

            Properties.Settings.Default.MonitorEnabled = false;
            Properties.Settings.Default.LogToFileEnabled = true;
            Properties.Settings.Default.Save();

            if (this.Icon != null)
            {
                notifyIcon1.Icon = this.Icon;
            }

            LoadSettings();
            InitializeTimer();
        }

        #region (新功能) 现代风格的文件夹选择器

        private string ShowModernFolderDialog(string title)
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Title = title;
                ofd.Filter = "Folders|*.none";
                ofd.FileName = "Select Folder";
                ofd.CheckFileExists = false;
                ofd.CheckPathExists = true;
                ofd.ValidateNames = false;

                if (!string.IsNullOrEmpty(_modernFolderDialogLastPath) && Directory.Exists(_modernFolderDialogLastPath))
                {
                    ofd.InitialDirectory = _modernFolderDialogLastPath;
                }
                else
                {
                    ofd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer);
                }

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    string folderPath = Path.GetDirectoryName(ofd.FileName);
                    _modernFolderDialogLastPath = folderPath;
                    return folderPath;
                }
            }
            return null;
        }

        #endregion

        #region 后台监控与托盘图标 (无修改)

        private void InitializeTimer()
        {
            _fileTimer = new System.Timers.Timer(Properties.Settings.Default.MonitorInterval);
            _fileTimer.Elapsed += FileTimer_Elapsed;
            _fileTimer.AutoReset = false;
            _fileTimer.Enabled = Properties.Settings.Default.MonitorEnabled;
        }

        private void RunMonitoringCheck(DateTime checkFrom, bool updateLastCheckTime)
        {
            try
            {
                string sourceFolder = Properties.Settings.Default.MonitorSourceFolder;
                string destFolder = Properties.Settings.Default.MonitorDestFolder;

                if (!Directory.Exists(sourceFolder) || !Directory.Exists(destFolder))
                {
                    Log($"[监控错误] 源文件夹或目标文件夹不存在。");
                    return;
                }

                Log($"[监控] 正在检查 {sourceFolder} (查找晚于 {checkFrom:yyyy-MM-dd HH:mm:ss} 的文件)...");

                var newFiles = Directory.GetFiles(sourceFolder, "*.txt")
                                        .Where(f => File.GetLastWriteTime(f) > checkFrom)
                                        .ToList();

                if (newFiles.Count > 0)
                {
                    Log($"[监控] 发现 {newFiles.Count} 个新文件，开始转移...");
                    foreach (string file in newFiles)
                    {
                        TransferFile(file, destFolder);
                    }
                }
                else
                {
                    Log($"[监控] 未发现新文件。");
                }

                if (updateLastCheckTime && Properties.Settings.Default.MonitorEnabled)
                {
                    Properties.Settings.Default.LastCheckTime = DateTime.Now;
                    Properties.Settings.Default.Save();
                }
            }
            catch (Exception ex)
            {
                Log($"[监控致命错误] {ex.Message}");
            }
        }

        private void FileTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                DateTime lastCheck = Properties.Settings.Default.LastCheckTime;
                RunMonitoringCheck(lastCheck, true);
            }
            finally
            {
                if (Properties.Settings.Default.MonitorEnabled)
                {
                    _fileTimer.Start();
                }
            }
        }

        private void TransferFile(string sourceFile, string destFolder)
        {
            try
            {
                string fileName = Path.GetFileName(sourceFile);
                string destFile = Path.Combine(destFolder, fileName);

                if (File.Exists(destFile))
                {
                    Log($"[监控] 跳过: {fileName} 已存在于目标文件夹。");
                }
                else
                {
                    File.Copy(sourceFile, destFile);
                    Log($"[监控] 成功复制: {fileName}");
                }
            }
            catch (Exception ex)
            {
                Log($"[监控文件转移错误] 复制 {sourceFile} 失败: {ex.Message}");
            }
        }

        private async void btnTestTransfer_Click(object sender, EventArgs e)
        {
            SaveSettings();

            Log("[手动测试] 正在启动一次性扫描 (从1970年开始)...");
            btnTestTransfer.Enabled = false;

            await Task.Run(() => {
                RunMonitoringCheck(new DateTime(1970, 1, 1), false);
            });

            Log("[手动测试] 测试完成。");
            btnTestTransfer.Enabled = true;
        }


        private void btnToggleMonitoring_Click(object sender, EventArgs e)
        {
            SaveSettings();

            bool isNowEnabled = !Properties.Settings.Default.MonitorEnabled;

            if (isNowEnabled)
            {
                if (Properties.Settings.Default.LogToFileEnabled &&
                    string.IsNullOrWhiteSpace(Properties.Settings.Default.LogFilePath))
                {
                    MessageBox.Show("“启用日志”已开启，请必须指定一个有效的“日志文件路径”。\n\n请在“日志设置”中指定路径后重试。",
                                    "启用失败",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    Log("[错误] 启用后台运行失败：日志已启用，但日志文件路径为空。");

                    Properties.Settings.Default.MonitorEnabled = false;
                    Properties.Settings.Default.Save();
                    btnToggleMonitoring.Text = "启用后台持续运行";
                    _fileTimer.Stop();

                    return;
                }

                Properties.Settings.Default.MonitorEnabled = true;

                Log($"[设置] 后台持续运行已启用。");
                Log($"    -> 间隔: {numInterval.Value} 秒");
                Log($"    -> 源: {Properties.Settings.Default.MonitorSourceFolder}");
                Log($"    -> 目标: {Properties.Settings.Default.MonitorDestFolder}");

                Properties.Settings.Default.LastCheckTime = new DateTime(1970, 1, 1);

                _fileTimer.Interval = Properties.Settings.Default.MonitorInterval;
                _fileTimer.Start();

                btnToggleMonitoring.Text = "关闭后台持续运行";
            }
            else
            {
                Properties.Settings.Default.MonitorEnabled = false;
                Log("[设置] 后台持续运行已停止。");
                _fileTimer.Stop();
                btnToggleMonitoring.Text = "启用后台持续运行";
            }

            Properties.Settings.Default.Save();
        }


        private void btnBrowseMonitorSource_Click(object sender, EventArgs e)
        {
            string selectedFolder = ShowModernFolderDialog("请选择 .txt 所在的源文件夹 (公共盘或本地)");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                txtMonitorSource.Text = selectedFolder;
            }
        }

        private void btnBrowseMonitorDest_Click(object sender, EventArgs e)
        {
            string selectedFolder = ShowModernFolderDialog("请选择 .txt 要转移到的目标文件夹");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                txtMonitorDest.Text = selectedFolder;
            }
        }

        private void numInterval_ValueChanged(object sender, EventArgs e)
        {
            int newInterval = (int)numInterval.Value * 1000;
            Properties.Settings.Default.MonitorInterval = newInterval;
            Properties.Settings.Default.Save();
            _fileTimer.Interval = newInterval;
            Log($"[设置] 监控间隔已更新为 {numInterval.Value} 秒。");
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (Properties.Settings.Default.MonitorEnabled && !_isExiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                notifyIcon1.ShowBalloonTip(1000, "程序正在后台运行", "DB 文件监控工具已最小化到托盘。", ToolTipIcon.Info);
            }
            else if (!_isExiting && e.CloseReason == CloseReason.UserClosing)
            {
                _isExiting = true;
                _fileTimer.Stop();
                notifyIcon1.Visible = false;
                Application.Exit();
            }
        }

        private void notifyIcon1_DoubleClick(object sender, EventArgs e) { ShowForm(); }
        private void menuItemShow_Click(object sender, EventArgs e) { ShowForm(); }
        private void menuItemExit_Click(object sender, EventArgs e)
        {
            _isExiting = true;
            _fileTimer.Stop();
            notifyIcon1.Visible = false;
            Application.Exit();
        }
        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        #endregion

        #region 设置的加载与保存 (已修改)

        private void LoadSettings()
        {
            txtInput.Text = Properties.Settings.Default.InputPath;
            txtOutput.Text = Properties.Settings.Default.OutputPath;
            txtImageDir.Text = Properties.Settings.Default.ImageDirPath;
            txtMonitorSource.Text = Properties.Settings.Default.MonitorSourceFolder;
            txtMonitorDest.Text = Properties.Settings.Default.MonitorDestFolder;

            if (Properties.Settings.Default.MonitorEnabled)
            {
                btnToggleMonitoring.Text = "关闭后台持续运行";
            }
            else
            {
                btnToggleMonitoring.Text = "启用后台持续运行";
            }

            int intervalSeconds = Properties.Settings.Default.MonitorInterval / 1000;
            if (intervalSeconds < numInterval.Minimum) intervalSeconds = (int)numInterval.Minimum;
            if (intervalSeconds > numInterval.Maximum) intervalSeconds = (int)numInterval.Maximum;
            numInterval.Value = intervalSeconds;

            chkLogToFile.Checked = Properties.Settings.Default.LogToFileEnabled;
            txtLogPath.Text = Properties.Settings.Default.LogFilePath;

            Log("已加载所有保存的路径和设置。");
        }

        // *** 修复：删除了那行错误的代码 ***
        private void SaveSettings()
        {
            Properties.Settings.Default.InputPath = txtInput.Text;
            Properties.Settings.Default.OutputPath = txtOutput.Text;
            Properties.Settings.Default.ImageDirPath = txtImageDir.Text;
            Properties.Settings.Default.MonitorSourceFolder = txtMonitorSource.Text;
            // 错误的那一行 'Opening `SaveSettings`' 已被删除
            Properties.Settings.Default.MonitorDestFolder = txtMonitorDest.Text;
            Properties.Settings.Default.MonitorInterval = (int)numInterval.Value * 1000;
            Properties.Settings.Default.LogToFileEnabled = chkLogToFile.Checked;
            Properties.Settings.Default.LogFilePath = txtLogPath.Text;

            Properties.Settings.Default.Save();
            Log("已保存当前所有路径和设置。");
        }

        #endregion

        #region 日志 (无修改)

        private void Log(string message)
        {
            string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";

            if (txtLog.InvokeRequired)
            {
                txtLog.Invoke((MethodInvoker)delegate {
                    txtLog.AppendText(logText + "\r\n");
                });
            }
            else
            {
                txtLog.AppendText(logText + "\r\n");
            }

            if (Properties.Settings.Default.LogToFileEnabled)
            {
                string logPath = Properties.Settings.Default.LogFilePath;
                if (string.IsNullOrWhiteSpace(logPath)) return;

                try
                {
                    lock (_logLock)
                    {
                        File.AppendAllText(logPath, logText + "\r\n");
                    }
                }
                catch (Exception ex)
                {
                    string errorMsg = $"[日志文件写入失败] {ex.Message}";
                    if (txtLog.InvokeRequired)
                    {
                        txtLog.Invoke((MethodInvoker)delegate {
                            txtLog.AppendText(errorMsg + "\r\n");
                        });
                    }
                    else
                    {
                        txtLog.AppendText(errorMsg + "\r\n");
                    }
                }
            }
        }

        private void chkLogToFile_CheckedChanged(object sender, EventArgs e)
        {
            SaveSettings();
        }

        private void btnBrowseLogPath_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt";
                sfd.DefaultExt = "log";
                sfd.FileName = "monitor_log.log";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    txtLogPath.Text = sfd.FileName;
                    SaveSettings();
                }
            }
        }


        #endregion

        #region 手动提取 (无修改)

        private void btnBrowseInput_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "DB 文件 (*.db)|*.db|所有文件 (*.*)|*.*";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    txtInput.Text = ofd.FileName;
                }
            }
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                sfd.DefaultExt = "txt";
                sfd.FileName = "output.txt";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    txtOutput.Text = sfd.FileName;
                }
            }
        }

        private void btnBrowseImageDir_Click(object sender, EventArgs e)
        {
            string selectedFolder = ShowModernFolderDialog("请选择输出图片目录");
            if (!string.IsNullOrEmpty(selectedFolder))
            {
                txtImageDir.Text = selectedFolder;
            }
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            btnStart.Enabled = false;
            txtLog.Clear();
            Log("开始手动转换...");

            Properties.Settings.Default.InputPath = txtInput.Text;
            Properties.Settings.Default.OutputPath = txtOutput.Text;
            Properties.Settings.Default.ImageDirPath = txtImageDir.Text;
            Properties.Settings.Default.Save();
            Log("已保存提取路径。");

            string inputPath = txtInput.Text;
            string outputPath = txtOutput.Text;
            string imageExportDir = txtImageDir.Text;

            try
            {
                await Task.Run(() => {
                    bool isSuccess = false;
                    Lib_InspectResultsOp.Model_InspectResult data = null;

                    try
                    {
                        data = SerializationLib.BinaryDeSerialization<Lib_InspectResultsOp.Model_InspectResult>(inputPath, ref isSuccess);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message);
                        isSuccess = false;
                    }

                    if (isSuccess && data != null)
                    {
                        using (StreamWriter writer = new StreamWriter(outputPath))
                        {
                            writer.WriteLine("=== 缺陷位置信息 ===");
                            writer.WriteLine($"横坐标(RowCenter): {data.RowCenter}");
                            writer.WriteLine($"纵坐标(ColCenter): {data.ColCenter}");
                            writer.WriteLine();
                            writer.WriteLine("=== 缺陷描述 ===");
                            foreach (var defect in data.DefectInformations ?? new List<string>())
                            {
                                writer.WriteLine($"- {defect}");
                            }
                            writer.WriteLine();
                            writer.WriteLine("=== 缺陷矩形框位置 ===");
                            if (data.AllDefectRectangle != null)
                            {
                                foreach (var rect in data.AllDefectRectangle)
                                {
                                    writer.WriteLine($"矩形框: X={rect.X}, Y={rect.Y}, " +
                                                     $"Width={rect.Width}, Height={rect.Height}");
                                }
                            }
                            writer.WriteLine();
                            writer.WriteLine("=== AI过滤信息 ===");
                            writer.WriteLine(data.AiFilterInformation ?? "无AI过滤信息");
                            writer.WriteLine();
                            writer.WriteLine("=== 图片信息 ===");

                            string failImageName = ImageExporter.ExportImage(data.FailImage, imageExportDir, "fail");
                            Log($"[提取] 缺陷图片: {(failImageName != null ? Path.Combine(imageExportDir, failImageName) : "不存在")}");
                            writer.WriteLine($"缺陷图片: {(failImageName != null ? Path.Combine(imageExportDir, failImageName) : "不存在")}");

                            string irImageName = ImageExporter.ExportImage(data.FailIRImage, imageExportDir, "ir");
                            Log($"[提取] IR图片: {(irImageName != null ? Path.Combine(imageExportDir, irImageName) : "不存在")}");
                            writer.WriteLine($"IR图片: {(irImageName != null ? Path.Combine(imageExportDir, irImageName) : "不存在")}");
                        }
                        data?.Dispose();
                        Log("手动数据导出完成！");
                    }
                    else
                    {
                        Log("手动反序列化失败，请检查输入文件是否存在且格式正确。");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"[提取致命错误] {ex.Message}");
            }
            finally
            {
                btnStart.Enabled = true;
            }
        }

        #endregion
    }
}