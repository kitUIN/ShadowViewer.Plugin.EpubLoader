using System;

namespace ShadowViewer.Plugin.EpubLoader.Exceptions;

/// <summary>
/// 导入失败
/// </summary>
public class EpubImportException : Exception
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    public EpubImportException(string message) : base(message)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="innerException"></param>
    public EpubImportException(string message, Exception innerException) : base(message, innerException)
    {
    }
}