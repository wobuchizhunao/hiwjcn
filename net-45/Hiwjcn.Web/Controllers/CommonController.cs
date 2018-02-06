﻿using Lib.core;
using Lib.helper;
using Lib.io;
using Lib.mvc;
using Lib.net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Xml;
using Lib.mvc.attr;
using Hiwjcn.Framework;
using System.Threading.Tasks;
using Lib.extra_;
using WebCore.MvcLib.Controller;
using Lib.extension;

namespace Hiwjcn.Web.Controllers
{
    /// <summary>
    /// 公用组件
    /// </summary>
    public class CommonController : EpcBaseController
    {
        [HttpPost]
        [EpcAuth]
        public async Task<ActionResult> Upload(string only_qiniu)
        {
            return await RunActionAsync(async () =>
            {
                if ((this.X.context.Request.Files?.Count ?? 0) <= 0)
                {
                    return GetJsonRes("请选择要上传的文件");
                }
                var file = this.X.context.Request.Files[0] ?? throw new ArgumentNullException("文件不存在");
                if (file.ContentLength > Com.MbToB(1))
                {
                    return GetJsonRes("文件过大");
                }

                var qiniu = new QiniuHelper();

                if ((only_qiniu ?? "false").ToBool())
                {
                    var url = qiniu.Upload(file.GetBytes(), Com.GetUUID());
                    return GetJson(new _() { success = true, data = url });
                }
                else
                {
                    var path = Server.MapPath("~/static/upload/");

                    var uploader = new FileUpload()
                    {
                        MaxSize = Com.MbToB(1)
                    };

                    var res = uploader.UploadSingleFile(file, path);
                    if (!res.SuccessUpload)
                    {
                        return GetJsonRes("上传失败");
                    }
                    var url = qiniu.Upload(res.FilePath, Com.GetUUID());

                    await Task.FromResult(1);
                    return GetJson(new _() { success = true, data = url });
                }
            });
        }
        [HttpPost]
        public ActionResult SetLang(string lang)
        {
            return RunAction(() =>
            {
                if (ValidateHelper.IsPlumpString(lang))
                {
                    this.X.context.SetCookie(LanguageHelper.CookieName, lang);
                }
                else
                {
                    this.X.context.RemoveCookie(new string[] { LanguageHelper.CookieName });
                }
                return GetJsonRes("");
            });
        }

        /// <summary>
        /// 站点地图
        /// </summary>
        /// <returns></returns>
        public ActionResult SiteMap()
        {
            return RunAction(() =>
            {
                var xml = new XmlDocument();
                xml.AppendChild(xml.CreateXmlDeclaration("1.0", "utf-8", null));
                XmlNode root = xml.CreateElement("urlset");
                //添加xmlns
                var attr = xml.CreateAttribute("xmlns");
                attr.Value = "http://www.sitemaps.org/schemas/sitemap/0.9";
                root.Attributes.Append(attr);

                xml.AppendChild(root);
                XmlNode urlNode = null;
                XmlNode node = null;

                {
                    #region 固定的几个
                    string[] staticurls = new string[]
                    {
                        this.X.BaseUrl+"page/home/",
                        this.X.BaseUrl+"post/postlist/",
                        this.X.BaseUrl+"shop/productlist/",
                    };
                    int index = 0;
                    staticurls.ToList().ForEach(url =>
                    {
                        urlNode = xml.CreateElement("url");

                        node = xml.CreateElement("loc");
                        node.InnerText = url;
                        urlNode.AppendChild(node);

                        node = xml.CreateElement("lastmod");
                        node.InnerText = DateTime.Now.ToString("yyyy-MM-dd");
                        urlNode.AppendChild(node);

                        node = xml.CreateElement("changefreq");
                        node.InnerText = "always";
                        urlNode.AppendChild(node);

                        node = xml.CreateElement("priority");
                        node.InnerText = (++index == 1) ? "1.0" : "0.9";
                        urlNode.AppendChild(node);

                        root.AppendChild(urlNode);
                    });
                    #endregion
                }

                {
                    //保存到本地
                    string savePath = ServerHelper.GetMapPath(this.X.context, "~/App_Data/sitemap.xml");
                    if (IOHelper.FileHelper.Exists(savePath))
                    {
                        IOHelper.FileHelper.Delete(savePath);
                    }
                    xml.Save(savePath);
                }

                using (var stream = new MemoryStream())
                {
                    xml.Save(stream);
                    //用完之后自动关闭
                    byte[] b = ConvertHelper.MemoryStreamToBytes(stream);
                    if (!ValidateHelper.IsPlumpList(b))
                    {
                        return Content("生成站点地图错误，请联系管理员");
                    }
                    return Content(System.Text.Encoding.UTF8.GetString(b), "text/xml");
                }
            });
        }

        /// <summary>
        /// 发送邮件处理程序
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [ValidateInput(false)]
        public ActionResult SendMailAction(string name, string email, string subject, string content)
        {
            return RunAction(() =>
            {
                if (!ValidateHelper.IsAllPlumpString(name, email, subject, content))
                {
                    return GetJsonRes("Please input correctly");
                }

                if (!ValidateHelper.IsEmail(email))
                {
                    return GetJsonRes("Please input a corret email address");
                }

                string sendcontent = string.Format("<p>From:{0}({1})</p>", name, email);
                sendcontent += string.Format("<p>{0}</p>", content);

                var model = new EmailModel() { Subject = subject, MailBody = sendcontent };

                var config = ConfigHelper.Instance;

                model.SmtpServer = config.SmptServer;
                model.UserName = config.SmptLoginName;
                model.Password = config.SmptPassWord;
                model.SenderName = config.SmptSenderName;
                model.Address = config.SmptEmailAddress;

                model.ToList = new List<string>() { config.FeedBackEmail };

                if (!ValidateHelper.IsEmail(model.Address))
                {
                    return GetJsonRes("发送邮件地址格式错误");
                }
                if (!ValidateHelper.IsAllPlumpString(
                    model.UserName,
                    model.Password,
                    model.SenderName))
                {
                    return GetJsonRes("发送参数设置不正确");
                }
                if (!(ValidateHelper.IsPlumpList(model.ToList) &&
                    model.ToList.Where(x => ValidateHelper.IsEmail(x)).Count() > 0))
                {
                    return GetJsonRes("接收邮箱未设置");
                }

                return GetJsonRes(EmailSender.SendMail(model) ? "" : "发送失败，请联系管理员");
            });
        }

        /// <summary>
        /// 验证码
        /// </summary>
        /// <returns></returns>
        //[CheckReferrerUrl]
        public ActionResult VerifyCode(string key)
        {
            return RunAction(() =>
            {
                if (new string[] { "login_verify", "reg_verify" }.Contains(key))
                {
                    return Http403();
                }
                var code = new DrawVerifyCode();
                byte[] bs = code.GetImageBytes();
                if (!ValidateHelper.IsPlumpList(bs)) { return Content("没有数据"); }

                this.X.context.Response.SetResponseNoCache();

                this.X.context.Session.SetSession(key, code.Code);
                return File(bs, "image/Png");
            });
        }

        /// <summary>
        /// 生成二维码
        /// </summary>
        /// <returns></returns>
        public ActionResult QrCode(string con, string i, string bar, string hue)
        {
            return RunAction(() =>
            {
                if (!ValidateHelper.IsLenInRange(con, 1, 500))
                {
                    return Content("长度超出范围");
                }
                var qr = new QrCode();
                string img = null;
                if (i?.Length > 0)
                {
                    img = Server.MapPath("~/Static/image/no_data.png");
                }
                var b = ValidateHelper.IsPlumpString(bar) ?
                qr.GetBarCodeBytes(con) : qr.GetQrCodeWithIconBytes(con, icon_path: img);

                if (ValidateHelper.IsPlumpString(hue))
                {
                    b = qr.AddRandomHue(b);
                }

                if (!ValidateHelper.IsPlumpList(b)) { return Content("bytes is empty"); }

                this.X.context.Response.SetResponseNoCache();

                return File(b, "image/Png");
            });
        }

        public ActionResult QrCodex(string con)
        {
            return RunAction(() =>
            {
                var qr = new QrCode();
                var b = qr.GetQrCodeBytes(con ?? string.Empty);
                
                this.X.context.Response.SetResponseNoCache();

                return File(b, "image/Png");
            });
        }
        
        /// <summary>
        /// 读取图片
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public ActionResult ReadImage(string id)
        {
            return RunAction(() =>
            {
                var b = Lib.io.IOHelper.GetFileBytes(id);
                if (!ValidateHelper.IsPlumpList<byte>(b)) { return Content("empty"); }

                this.X.context.Response.SetResponseNoCache();

                return File(b, "Image/Png");
            });
        }

        /// <summary>
        /// 识别二维码
        /// </summary>
        /// <returns></returns>
        public ActionResult ReadQrCode()
        {
            return RunAction(() =>
            {
                var file = this.X.context.Request.Files["img"];
                if (file == null) { return Content(string.Empty); }
                byte[] b = IOHelper.GetPostFileBytesAndDispose(file);
                if (!ValidateHelper.IsPlumpList(b)) { return Content(string.Empty); }
                QrCode qr = new QrCode();
                string str = qr.DistinguishQrImage(b);
                if (ValidateHelper.IsPlumpString(str))
                {
                    return Content(str);
                }
                else
                {
                    return Content("未能识别出内容");
                }
            });
        }

        /// <summary>
        /// 读取所有css
        /// </summary>
        /// <returns></returns>
        public ActionResult AllCss()
        {
            return RunAction(() =>
            {
                StringBuilder css = new StringBuilder();
                string base_path = Server.MapPath("~/ui/res/css/");
                string[] files = IOHelper.DirectoryHelper.ListFiles(base_path);
                if (ValidateHelper.IsPlumpList(files))
                {
                    files.ToList().ForEach(delegate (string file)
                    {
                        file = ConvertHelper.GetString(file);
                        if (!file.ToLower().EndsWith(".css"))
                        {
                            return;
                        }
                        css.Append(IOHelper.ReadFileString(file));
                    });
                }
                return Content(css.ToString(), "text/css");
            });
        }

        /// <summary>
        /// 下载文件
        /// </summary>
        /// <returns></returns>
        public ActionResult DownLoad(string name, string path)
        {
            return RunAction(() =>
            {
                string filename = name;
                string filepath = path;
                if (!ValidateHelper.IsAllPlumpString(filename, filepath))
                {
                    return Content("文件不存在");
                }
                /*
                context.Response.ContentType = "application/octet-stream";
                context.Response.AddHeader("content-disposition", "attachment;filename=" + filename);
                context.Response.BinaryWrite(lib.io.IOHelper.getFileBytes(filepath));
                */
                string sep = IOHelper.GetSysPathSeparator();
                filepath = Server.MapPath("~" + sep + "static" + sep + "download" + sep + filepath);
                byte[] b = null;
                if (IOHelper.FileHelper.Exists(filepath))
                {
                    b = IOHelper.GetFileBytes(filepath);
                }
                if (!ValidateHelper.IsPlumpList(b))
                {
                    return Content("文件不存在");
                }
                return File(b, "application/octet-stream", filename);
            });
        }

    }
}
