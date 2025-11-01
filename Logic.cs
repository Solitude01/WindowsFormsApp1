using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

// 命名空间 Lib_InspectResultsOp 是故意保留的，用于正确反序列化 .db 文件
namespace Lib_InspectResultsOp
{
    [Serializable]
    public class Model_InspectResult : ISerializable, IDisposable
    {
        public Bitmap FailImage { get; set; }
        public Bitmap FailIRImage { get; set; }
        public int ColCenter { get; set; }
        public int RowCenter { get; set; }
        public List<string> DefectInformations { get; set; }
        public List<Rectangle> AllDefectRectangle { get; set; }
        public string AiFilterInformation { get; set; }

        public Model_InspectResult() { }

        // 最终修正 v4:
        //  - FailImage:     Key="failImage",   Type=Bitmap
        //  - FailIRImage:   Key="failIRImage", Type=byte[]
        //  - 其他字段:      Key=k__BackingField
        protected Model_InspectResult(SerializationInfo info, StreamingContext context)
        {
            string backingField(string propName) => $"<{propName}>k__BackingField";
            Bitmap BytesToBitmap(byte[] bytes)
            {
                if (bytes == null || bytes.Length == 0) return null;
                using (var ms = new MemoryStream(bytes))
                {
                    return new Bitmap(ms);
                }
            }
            try
            {
                FailImage = (Bitmap)info.GetValue("failImage", typeof(Bitmap));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"反序列化FailImage失败: {ex.Message}");
            }
            try
            {
                FailIRImage = BytesToBitmap((byte[])info.GetValue("failIRImage", typeof(byte[])));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"反序列化FailIRImage失败: {ex.Message}");
            }
            try { ColCenter = info.GetInt32(backingField("ColCenter")); } catch (Exception ex) { Console.WriteLine($"反序列化ColCenter失败: {ex.Message}"); }
            try { RowCenter = info.GetInt32(backingField("RowCenter")); } catch (Exception ex) { Console.WriteLine($"反序列化RowCenter失败: {ex.Message}"); }
            try { DefectInformations = (List<string>)info.GetValue(backingField("DefectInformations"), typeof(List<string>)); } catch (Exception ex) { Console.WriteLine($"反序列化DefectInformations失败: {ex.Message}"); }
            try { AllDefectRectangle = (List<Rectangle>)info.GetValue(backingField("AllDefectRectangle"), typeof(List<Rectangle>)); } catch (Exception ex) { Console.WriteLine($"反序列化AllDefectRectangle失败: {ex.Message}"); }
            try { AiFilterInformation = info.GetString(backingField("AiFilterInformation")); } catch (Exception ex) { Console.WriteLine($"反序列化AiFilterInformation失败: {ex.Message}"); }
        }
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            string backingField(string propName) => $"<{propName}>k__BackingField";
            byte[] BitmapToBytes(Bitmap bmp)
            {
                if (bmp == null) return null;
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    return ms.ToArray();
                }
            }
            info.AddValue("failImage", FailImage);
            info.AddValue("failIRImage", BitmapToBytes(FailIRImage));
            info.AddValue(backingField("ColCenter"), ColCenter);
            info.AddValue(backingField("RowCenter"), RowCenter);
            info.AddValue(backingField("DefectInformations"), DefectInformations);
            info.AddValue(backingField("AllDefectRectangle"), AllDefectRectangle);
            info.AddValue(backingField("AiFilterInformation"), AiFilterInformation);
        }
        public void Dispose()
        {
            FailImage?.Dispose();
            FailIRImage?.Dispose();
        }
    }
}


// UBinder 类
public class UBinder : SerializationBinder
{
    public override Type BindToType(string assemblyName, string typeName)
    {
        if (typeName.Contains("Lib_InspectResultsOp.Model_InspectResult"))
        {
            string currentAssembly = Assembly.GetExecutingAssembly().FullName;
            return Type.GetType($"{typeName}, {currentAssembly}");
        }
        Type typeToFind = null;
        try
        {
            typeToFind = Type.GetType($"{typeName}, {assemblyName}", true);
        }
        catch (Exception)
        {
            try
            {
                string simpleAssemblyName = assemblyName.Split(',')[0].Trim();
                typeToFind = Type.GetType($"{typeName}, {simpleAssemblyName}", true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UBinder 致命错误: 无法加载类型 '{typeName}' (来自程序集 '{assemblyName}'). 错误: {ex.Message}");
            }
        }
        return typeToFind;
    }
}


// SerializationLib 类
public class SerializationLib
{
    public static readonly object locks = new object();
    public static T BinaryDeSerialization<T>(string filepath, ref bool result) where T : new()
    {
        result = false;
        T t = new T();
        if (File.Exists(filepath))
        {
            try
            {
                lock (locks)
                {
                    using (Stream st = new FileStream(filepath, FileMode.Open))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        bf.Binder = new UBinder();
                        t = (T)bf.Deserialize(st);
                    }
                }
                result = true;
            }
            catch (Exception e)
            {
                // 抛出异常，由 UI 线程捕获
                throw new Exception($"反序列化错误: {e.Message}");
            }
        }
        return t;
    }
}

// ImageExporter 类
public class ImageExporter
{
    public static string ExportImage(Bitmap image, string exportDir, string prefix)
    {
        if (image == null) return null;
        try
        {
            if (!Directory.Exists(exportDir))
            {
                Directory.CreateDirectory(exportDir);
            }
            string fileName = $"{prefix}_{DateTime.Now:yyyyMMddHHmmssfff}.png";
            string filePath = Path.Combine(exportDir, fileName);
            image.Save(filePath, ImageFormat.Png);
            return fileName;
        }
        catch (Exception ex)
        {
            // 抛出异常，由 UI 线程捕获
            throw new Exception($"图片导出失败: {ex.Message}");
        }
    }
}