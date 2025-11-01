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

// 命名空间已更新为 WindowsFormsApp1
namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            LoadSettings(); // 窗体启动时加载已保存的路径
        }

        // 记录日志到界面上的文本框 (已修正 InvokeRequired 问题)
        private void Log(string message)
        {
            // 准备好要写入的文本
            string logText = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";

            // 检查：我们是否在“非 UI 线程”上？
            if (txtLog.InvokeRequired)
            {
                // 是的，我们在后台线程（例如 Task.Run）
                // 必须使用 Invoke 来安全地跨线程更新 UI
                txtLog.Invoke((MethodInvoker)delegate {
                    txtLog.AppendText(logText);
                });
            }
            else
            {
                // 不是，我们就在 UI 线程上
                // (例如程序刚启动，或在按钮点击事件中)
                // 我们可以（也必须）直接更新 UI
                txtLog.AppendText(logText);
            }
        }

        // 加载上次保存的路径
        private void LoadSettings()
        {
            txtInput.Text = Properties.Settings.Default.InputPath;
            txtOutput.Text = Properties.Settings.Default.OutputPath;
            txtImageDir.Text = Properties.Settings.Default.ImageDirPath;
            Log("已加载上次保存的路径。");
        }

        // 保存当前路径
        private void SaveSettings()
        {
            Properties.Settings.Default.InputPath = txtInput.Text;
            Properties.Settings.Default.OutputPath = txtOutput.Text;
            Properties.Settings.Default.ImageDirPath = txtImageDir.Text;
            Properties.Settings.Default.Save();
            Log("已保存当前路径。");
        }

        // “...” 浏览输入文件
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

        // “...” 浏览输出文本文件
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

        // “...” 浏览图片输出目录
        private void btnBrowseImageDir_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtImageDir.Text = fbd.SelectedPath;
                }
            }
        }

        // “开始转换” 按钮
        private async void btnStart_Click(object sender, EventArgs e)
        {
            // 禁用按钮，防止重复点击
            btnStart.Enabled = false;
            txtLog.Clear();
            Log("开始转换...");

            // 1. 保存当前设置
            try
            {
                SaveSettings();
            }
            catch (Exception ex)
            {
                Log($"错误：保存设置失败。{ex.Message}");
                btnStart.Enabled = true;
                return;
            }

            // 2. 从界面获取路径
            string inputPath = txtInput.Text;
            string outputPath = txtOutput.Text;
            string imageExportDir = txtImageDir.Text;

            // 3. 在后台线程执行耗时操作，防止界面卡死
            try
            {
                await Task.Run(() => {
                    bool isSuccess = false;
                    Lib_InspectResultsOp.Model_InspectResult data = null; // 确保使用完整命名空间

                    try
                    {
                        data = SerializationLib.BinaryDeSerialization<Lib_InspectResultsOp.Model_InspectResult>(inputPath, ref isSuccess);
                    }
                    catch (Exception ex)
                    {
                        Log(ex.Message); // 记录反序列化错误
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
                            Log($"缺陷图片: {(failImageName != null ? Path.Combine(imageExportDir, failImageName) : "不存在")}");
                            writer.WriteLine($"缺陷图片: {(failImageName != null ? Path.Combine(imageExportDir, failImageName) : "不存在")}");

                            string irImageName = ImageExporter.ExportImage(data.FailIRImage, imageExportDir, "ir");
                            Log($"IR图片: {(irImageName != null ? Path.Combine(imageExportDir, irImageName) : "不存在")}");
                            writer.WriteLine($"IR图片: {(irImageName != null ? Path.Combine(imageExportDir, irImageName) : "不存在")}");
                        }

                        // 释放图片内存
                        data?.Dispose();
                        Log("数据导出完成！");
                    }
                    else
                    {
                        Log("反序列化失败，请检查输入文件是否存在且格式正确。");
                    }
                });
            }
            catch (Exception ex)
            {
                Log($"转换过程中发生致命错误: {ex.Message}");
            }
            finally
            {
                // 无论成功与否，都重新启用按钮
                btnStart.Enabled = true;
            }
        }
    }
}