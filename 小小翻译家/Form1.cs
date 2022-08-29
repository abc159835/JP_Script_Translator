using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Web;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using System.Windows.Forms.Design.Behavior;
using System.Xml;
using HtmlAgilityPack;
using Microsoft.VisualBasic;
using Newtonsoft.Json;

namespace 小小翻译家
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            //跨线程访问UI
            CheckForIllegalCrossThreadCalls = false;
            //初始化编码表
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
        //config为控件文本 json_data为翻译历史 dict_data为输出字典
        public Dictionary<string, string> config = new Dictionary<string, string>();
        public Dictionary<string, string> json_data = new Dictionary<string, string>();
        public Dictionary<string, string> dict_data = new Dictionary<string, string>();
        public int max_num = 0;
        public int press_button = 0;
        public bool stop = false;
        Thread th;
        private void button1_Click(object sender, EventArgs e)
        {
            stop = false;
            button1.Enabled = false;
            th = new Thread(main);
            th.IsBackground = true;
            th.Start();
        }
        public void main()
        {
            try
            {
                //统计
                int len = 0;
                int onlylen = 0;
                int charlen = 0;
                //data是用于去重的字典
                Dictionary<string, string> data = new Dictionary<string, string>();
                string filepath = textBox1.Text.Replace("\"", "");
                string outputdir = textBox2.Text.Replace("\"", "");
                //charlist是过滤字符的白名单
                string charlist = textBox10.Text;
                string read_code = comboBox1.Text;
                string write_code = comboBox2.Text;
                if (!outputdir.EndsWith('\\'))
                    outputdir += "\\";
                string[] jump = spiltchar(textBox12.Text, '|');
                string[] only = spiltchar(textBox11.Text, '|');
                string[] ends = spiltchar(textBox5.Text, '|');
                //用于替换的字典
                Dictionary<string, string> swap = read_dict(textBox4.Text);
                //用于翻译的字典
                Dictionary<string, string> fanyi_dict = read_dict(textBox3.Text);
                FileInfo[] Filelist = return_all_files(filepath);
                foreach (var file in Filelist)
                {
                    StringBuilder builder = new StringBuilder();
                    //输出完整路径+文件后缀不匹配
                    if (!isit(file.Extension, ends))
                    {
                        if (button1.Text == "下一步"&& checkBox8.Checked)
                            file.CopyTo(outputdir + file.Name, true);
                        continue;
                    }
                    else
                    {
                        if (!CheckIsTextFile(file.FullName)) 
                        {
                            string output = "";
                            //写入文件
                            if (checkBox7.Checked)
                                output = file.FullName;
                            else
                                output = outputdir + file.Name;
                            if (checkBox12.Checked)
                                output = output.Replace(file.Name, Tran(comboBox3.Text, file.Name));
                            if (checkBox7.Checked)
                                file.MoveTo(output,true);
                            else
                                file.CopyTo(output,true);
                        }
                    }
                    //文件编码判断
                    Encoding encoding = Encoding.Default;
                    if (read_code == "UTF-8")
                        encoding = GetEncoding(new FileStream(file.FullName, FileMode.Open));
                    else
                        encoding = Encoding.GetEncoding(read_code);
                    //读取文件
                    nowla.Text = "状态:正在分析文件|" + file.Name;
                    StreamReader sr = new StreamReader(new FileStream(file.FullName, FileMode.Open), encoding);
                    string line = "";
                    //贯彻始末
                    string swap_line = "";
                    while ((line = sr.ReadLine()) != null)
                    {
                    retry:
                        swap_line = line;
                        if (stop)
                        {
                            sr.Close();
                            return;
                        }
                        //判断jump和only
                        bool on_continue_jump = false;
                        bool on_continue_only = true;
                        if(only.Length == 0)
                            on_continue_only = false;
                        foreach (string li in jump)
                            if (line.Contains(li))
                                on_continue_jump = true;
                        foreach (string li in only)
                            if (line.Contains(li))
                                on_continue_only = false;
                        if (on_continue_jump|| on_continue_only)
                        {
                            //保证输出文件的完整
                            builder.AppendLine(line);
                            continue;
                        }
                        //使用字典翻译
                        foreach (var key in fanyi_dict)
                            if (line.Contains(key.Key))
                                swap_line = swap_line.Replace(key.Key, key.Value);
                        //简繁强制转换
                        if (checkBox6.Checked)
                            line = TostrSimple(line);
                        else if (checkBox5.Checked)
                            line = TostrTraditional(line);
                        //用于替换还原的字典
                        Dictionary<string, string[]> swap_dict = new Dictionary<string, string[]>();
                        bool on_post = false;
                        bool update = false;
                        string index = "";
                        string tran_line = "";
                        string ana_line = "";
                        int locate = 0;
                        if ((comboBox3.Text != "不翻译" && button1.Text == "下一步") || button1.Text == "开始")
                        {
                            //替换
                            foreach (var keys in swap.Keys)
                            {
                                Dictionary<string, string[]> lines = new Dictionary<string, string[]>();
                                if (keys.Contains('~'))
                                {
                                    string[] dobule = keys.Split('~');
                                    if (dobule[0] != "" && dobule[1] != "")
                                        lines = replace(dobule[0], dobule[1], swap[keys], swap_line);
                                }
                                else if(keys.Contains("Regex:"))
                                {
                                    lines = replace_regex(keys.Replace("Regex:",""), swap[keys], swap_line);
                                }
                                else 
                                {
                                    lines = replace_all(keys, swap[keys], swap_line);
                                }
                                foreach (string[] res in lines.Values)
                                    swap_dict[swap[keys]] = res;
                                foreach (string res in lines.Keys)
                                    swap_line = res;
                            }
                            string line_ = swap_line;
                            foreach (char c in line_)
                            {
                                locate++;
                                string lang = isCJK(c.ToString());
                                if (isthischar(charlist, c))
                                    index += c;
                                else if (lang == "zh")
                                {
                                    if (checkBox4.Checked)  //过滤中文字符
                                        on_post = true;
                                    index += c;
                                }
                                else if (lang == "jp")
                                {
                                    on_post = true;
                                    index += c;
                                }
                                else if (IsNumAndEnCh(c.ToString()))
                                {
                                    if (!checkBox10.Checked)
                                        index += c;
                                }
                                else
                                {
                                    if (on_post)
                                        update = true;
                                    else
                                        index = "";
                                }
                                if (on_post && locate == line_.Length)
                                    update = true;
                                if (update)
                                {
                                    //不翻译长度不足的文本
                                    if (getChinese(index).Length <= Convert.ToInt32(textBox9.Text))
                                        break;
                                    if (button1.Text == "开始")
                                    {
                                        if (!data.ContainsKey(index))
                                        {
                                            data.Add(index, "");
                                            onlylen++;
                                            charlen += index.Length;
                                            label12.Text = "进度:0/" + onlylen;
                                            System.Windows.Forms.Application.DoEvents();
                                        }
                                    }
                                    else if (button1.Text == "下一步")
                                    {
                                        string tran_ = "";
                                        ana_line += index + '|';
                                        if (!data.ContainsKey(index))
                                        {
                                            data.Add(index, "");
                                            onlylen++;
                                        }
                                        tran_ = Tran(comboBox3.Text, index);
                                        if (checkBox5.Checked)
                                            tran_ = TostrTraditional(tran_);
                                        swap_line = swap_line.Replace(index, tran_);
                                        tran_line += tran_ + '|';
                                    }
                                    len++;
                                    on_post = false;
                                    update = false;
                                    index = "";
                                }
                            }
                        }
                        if (button1.Text == "下一步")
                        {
                            //说明有分析结果
                            if (ana_line != "")
                            {
                                string res_line = reduction(swap_dict, swap_line);
                                ori_text.Text = line.Trim();
                                ana_text.Text = ana_line.Substring(0, ana_line.Length - 1);
                                tran_text.Text = tran_line.Substring(0, tran_line.Length - 1);
                                //不一致报错
                                if (res_line == "error") 
                                {
                                    MessageBox.Show("error:文本替换数目变化,返回原文!");
                                    res_text.Text = line.Trim();
                                }
                                else
                                    res_text.Text = res_line.Trim();
                                //不开启自动模式
                                if (!checkBox11.Checked)
                                {
                                    //重试或提交回来的恢复
                                    if(press_button != 0)
                                    {
                                        button2.Enabled = true;
                                        button3.Enabled = true;
                                        press_button = 0;
                                    }
                                    //阻塞线程
                                    while (press_button == 0)
                                    {
                                        if (stop)
                                        {
                                            sr.Close();
                                            return;
                                        }
                                        Thread.Sleep(100);
                                    }
                                    //按下重试
                                    if (press_button == 1)
                                        goto retry;
                                }
                                //写入原句
                                if (res_line == "error")
                                    builder.AppendLine(line);
                                else
                                {
                                    //写入用户更改的文本且保持缩进
                                    string nextlines = "";
                                    foreach(char nextline in line)
                                    {
                                        if (nextline == '\t')
                                            nextlines += "\t";
                                        else
                                            break;
                                    }
                                    builder.AppendLine(nextlines + res_text.Text);
                                }
                                //开启字典输出
                                if (checkBox1.Checked)
                                    if (!dict_data.ContainsKey(line.Trim()))
                                        dict_data.Add(line.Trim(), res_line.Trim());
                                label12.Text = "进度:" + onlylen + "/" + max_num;
                                progressBar1.Value = onlylen;
                                System.Windows.Forms.Application.DoEvents();
                                tran_line = "";
                                ana_line = "";
                            }
                            else
                                builder.AppendLine(line);
                        }
                    }
                    sr.Close();
                    if (button1.Text == "下一步" && outputdir != "\\")
                    {
                        string output = "";
                        //写入文件
                        if (write_code == "UTF-8")
                            encoding = GetEncoding(new FileStream(file.FullName, FileMode.Open));
                        encoding = Encoding.GetEncoding(write_code);
                        if (checkBox7.Checked)
                            output = file.FullName;
                        else
                            output = outputdir + file.Name;
                        if (checkBox12.Checked)
                            output = output.Replace(file.Name, Tran(comboBox3.Text, file.Name));
                        StreamWriter sw = new StreamWriter(new FileStream(output, FileMode.Create), encoding);
                        sw.Write(builder.ToString());
                        sw.Close();
                    }
                }
                if (button1.Text == "开始")
                {
                    progressBar1.Maximum = onlylen;
                    button1.Text = "下一步";
                    max_num = onlylen;
                    string message = string.Format("共找到{0}条文本\n长度为{1}\n已去除重负{3}条\n全部共计{2}条", onlylen, charlen, len, (len - onlylen));
                    MessageBox.Show(message);
                }
                else if (button1.Text == "下一步")
                {
                    button1.Text = "开始";
                    nowla.Text = "状态:";
                }
                button1.Enabled = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
        /// <summary>
        /// Checks the file is textfile or not.
        /// </summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        public static bool CheckIsTextFile(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            bool isTextFile = true;
            try
            {
                int i = 0;
                int length = (int)fs.Length;
                byte data;
                while (i < length && isTextFile)
                {
                    data = (byte)fs.ReadByte();
                    isTextFile = (data != 0);
                    i++;
                }
                return isTextFile;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (fs != null)
                {
                    fs.Close();
                }
            }
        }
        public string Tran(string option,string index)
        {
            re_tran:
            string tran_ = index;
            if (!json_data.ContainsKey(index))
            {
                try
                {
                    if (option == "百度翻译API")
                        tran_ = Tran_Baidu(index);
                    else if (option == "谷歌翻译")
                        tran_ = Tran_Google(index);
                    else if (option == "本地服务端")
                        tran_ = Tran_(index);
                    json_data.Add(index, tran_);
                    //翻译时间间隔
                    Thread.Sleep(Convert.ToInt32(textBox8.Text));
                }
                catch (Exception ex)
                {
                    MessageBox.Show("无法翻译"+index+",请检查网络！");
                    tran_ = index;
                }
            }
            else
            {
                tran_ = json_data[index];
                if (tran_ == "")
                {
                    json_data.Remove(index);
                    goto re_tran;
                }
            }
            return tran_;
        }
        public string Tran_Baidu(string q)
        {
            // 源语言
            string from = "auto";
            // 目标语言
            string to = "zh";
            // 改成您的APP ID
            string appId = textBox6.Text;
            Random rd = new Random();
            string salt = rd.Next(100000).ToString();
            // 改成您的密钥
            string secretKey = textBox7.Text;
            string sign = EncryptString(appId + q + salt + secretKey);
            string url = "http://api.fanyi.baidu.com/api/trans/vip/translate?";
            url += "q=" + HttpUtility.UrlEncode(q);
            url += "&from=" + from;
            url += "&to=" + to;
            url += "&appid=" + appId;
            url += "&salt=" + salt;
            url += "&sign=" + sign;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.ContentType = "text/html;charset=UTF-8";
            request.UserAgent = null;
            request.Timeout = 6000;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            Stream myResponseStream = response.GetResponseStream();
            StreamReader myStreamReader = new StreamReader(myResponseStream, Encoding.GetEncoding("utf-8"));
            string retString = myStreamReader.ReadToEnd();
            myStreamReader.Close();
            myResponseStream.Close();
            int a = retString.IndexOf("dst");
            int b = retString.IndexOf("}]}");
            return DecodeString(retString.Substring(a + 6, b - a - 7));
        }
        // 计算MD5值
        public static string EncryptString(string str)
        {
            MD5 md5 = MD5.Create();
            // 将字符串转换成字节数组
            byte[] byteOld = Encoding.UTF8.GetBytes(str);
            // 调用加密方法
            byte[] byteNew = md5.ComputeHash(byteOld);
            // 将加密结果转换为字符串
            StringBuilder sb = new StringBuilder();
            foreach (byte b in byteNew)
            {
                // 将字节转换成16进制表示的字符串，
                sb.Append(b.ToString("x2"));
            }
            // 返回加密的字符串
            return sb.ToString();
        }
        public string Tran_Google(string str)
        {
            WebClient wc = new WebClient();
            wc.Encoding = Encoding.UTF8;
            var content = wc.DownloadString("https://translate.google.cn/m?sl=auto&tl=zh-CN&hl=zh-CN&q=" + str);
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(content);
            HtmlNode comment = doc.DocumentNode.SelectSingleNode("/html/body/div/div[4]/text()");
            return comment.InnerText;
        }
        public string Tran_(string str)
        {
            WebClient wc = new WebClient();
            wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            var b = Encoding.UTF8.GetBytes("str="+str);
            var res = wc.UploadData(@"http://localhost:15983/", "post", b);
            var result = Encoding.UTF8.GetString(res);
            return result;
        }
        /// <summary>
        /// 判断输入的字符串是否只包含数字和英文字母
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static bool IsNumAndEnCh(string input)
        {
            string pattern = @"^[A-Za-z0-9]+$";
            Regex regex = new Regex(pattern);
            return regex.IsMatch(input);
        }
        public bool isthischar(string chars,char str)
        {
            foreach(char a in chars)
            {
                if (str == a)
                    return true;
            }
            return false;
        }
        public bool isit(string str, string[] c)
        {
            if (c.Length == 0)
                return true;
            foreach (string a in c)
            {
                if (str == a)
                    return true;
            }
            return false;
        }
        public string reduction(Dictionary<string, string[]> res, string str)
        {
            foreach (var key in res.Keys)
            {
                int count = 0;
                while (str.Contains(key))
                {
                    if (key.Length == 0)
                        break;
                    int a = str.IndexOf(key);
                    string start = str.Substring(0, a);
                    string ends = "";
                    if (str.Length >= a + key.Length)
                        ends = str.Substring(a + key.Length);
                    str = start + res[key][count] + ends;
                    count++;
                }
                if (count != res[key].Length)
                    return "error";
            }
            return str;
        }
        public Dictionary<string, string[]> replace(string begin, string end, string swaptext,string str)
        {
            Dictionary<string, string[]> res = new Dictionary<string, string[]>();
            string[] content = new string[] {};
            while (str.Contains(begin) && str.Contains(end))
            {
                int a = str.IndexOf(begin);
                int b = str.IndexOf(end, a + 1);
                if (b > a && a != -1)
                {
                    string s = str.Substring(a, end.Length + b - a);
                    string start = str.Substring(0, a);
                    string ends = "";
                    if (str.Length > b)
                        ends = str.Substring(b + 1);
                    str = start + swaptext + ends;
                    content = content.Append(s).ToArray();
                }
                else
                    break;
            }
            res.Add(str, content);
            return res;
        }
        public Dictionary<string, string[]> replace_regex(string regex, string swaptext, string str)
        {
            string text = Regex.Match(str,regex).Value;
            Dictionary<string, string[]> res = new Dictionary<string, string[]>();
            string[] content = new string[] {};
            while (str.Contains(text))
            {
                if (text.Length == 0)
                    break;
                int a = str.IndexOf(text);
                string s = str.Substring(a, text.Length);
                string start = str.Substring(0, a);
                string ends = "";
                if (str.Length >= a + s.Length)
                    ends = str.Substring(a + s.Length);
                str = start + swaptext + ends;
                content = content.Append(s).ToArray();
            }
            res.Add(str, content);
            return res;
        }
        public Dictionary<string, string[]> replace_all(string text, string swaptext, string str)
        {
            Dictionary<string, string[]> res = new Dictionary<string, string[]>();
            string[] content = new string[] {};
            while (str.Contains(text))
            {
                if (text.Length == 0)
                    break;
                int a = str.IndexOf(text);
                string s = str.Substring(a, text.Length);
                string start = str.Substring(0, a);
                string ends = "";
                if (str.Length >= a + s.Length)
                    ends = str.Substring(a + s.Length);
                str = start + swaptext + ends;
                content = content.Append(s).ToArray();
            }
            res.Add(str, content);
            return res;
        }
        /// <summary>
        /// 遍历文件夹下的所有文件
        /// </summary>
        /// <param name="path">文件夹路径，如C:\\Users\\Administrator\\Desktop\\</param>
        /// <param name="FileList">声明一个数组</param>
        /// <returns>字符串数组</returns>
        public static FileInfo[] GetFile(string path, FileInfo[] FileList)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            // 文件
            FileInfo[] fil = dir.GetFiles();
            // 文件夹
            DirectoryInfo[] dii = dir.GetDirectories();
            // 遍历文件
            foreach (FileInfo f in fil)
            {
                FileList = FileList.Append(f).ToArray();   // 添加文件路径到列表中
            }
            // 获取子文件夹内的文件列表，递归遍历
            foreach (DirectoryInfo d in dii)
            {
                foreach(var f in GetFile(d.FullName, new FileInfo[] { }))
                    FileList = FileList.Append(f).ToArray();
            }
            return FileList;
        }
        /// <summary>
        /// 取得CJK
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string getChinese(string str)
        {
            //声明存储结果的字符串
            string chineseString = "";
            //将传入参数中的中文字符添加到结果字符串中
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] >= 0x4E00 && str[i] <= 0x9FA5) //汉字
                {
                    chineseString += str[i];
                }
                if (str[i] >= 0x3040 && str[i] <= 0x30FF) //假名
                {
                    chineseString += str[i];
                }
            }
            //返回保留中文的处理结果
            return chineseString;
        }
        //判断字符串中是否有CJK，true有，false没有。
        public string isCJK(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (str[i] >= 0x4E00 && str[i] <= 0x9FA5) //汉字
                    return "zh";
                else if (str[i] >= 0x3040 && str[i] <= 0x30FF) //假名
                    return "jp";
            }
            return "";
        }
        public static string DecodeString(string unicode)
        {
            try
            {
                if (string.IsNullOrEmpty(unicode))
                {
                    return string.Empty;
                }
                return System.Text.RegularExpressions.Regex.Unescape(unicode);
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            string[] encodeing = { "UTF-8", "GBK", "Shift-JIS", "ASCII" };
            string[] tran = {"谷歌翻译" ,"百度翻译API","本地服务端","不翻译"};
            comboBox1.Items.AddRange(encodeing);
            comboBox2.Items.AddRange(encodeing);
            comboBox3.Items.AddRange(tran);
            comboBox1.SelectedIndex = 0;
            comboBox2.SelectedIndex = 0;
            comboBox3.SelectedIndex = 0;
            //还原控件文本
            config = read_dict("config.json");
            foreach(Control t in this.Controls)
                if (config.ContainsKey(t.Name))
                    t.Text = config[t.Name];
            if (!File.Exists("swap.json"))
                File.WriteAllText("swap.json", "{}");
            if (!File.Exists("data.json"))
                File.WriteAllText("data.json", "{}");
            if (checkBox9.Checked)
                json_data = read_dict("data.json");
        }

        private void checkBox6_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox6.Checked)
            {
                checkBox5.Checked = false;
            }
        }

        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox5.Checked)
            {
                checkBox6.Checked = false;
            }
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox7.Checked)
            {
                checkBox8.Checked = false;
                textBox2.Enabled = false;
            }
            else
            {
                textBox2.Enabled = true;
                checkBox8.Checked = true;
            }
        }
        /// <summary>
        /// 中文简繁转换
        /// </summary>
        /// <param name="x">中文</param>
        /// <param name="type">0.转繁体  1.转简体</param>
        /// <returns></returns>
        #region 简繁体转换
        /// <summary>
        /// 字符串简体转繁体  1033参数是防止转换后中文乱码
        /// </summary>
        /// <param name="strSimple"></param>
        /// <returns></returns>
        public static string TostrTraditional(string strSimple)
        {
            string strTraditional = Microsoft.VisualBasic.Strings.StrConv(strSimple, Microsoft.VisualBasic.VbStrConv.TraditionalChinese, 1033);
            return strTraditional;
        }

        /// <summary>
        /// 字符串繁体转简体
        /// </summary>
        /// <param name="strTraditional"></param>
        /// <returns></returns>
        public static string TostrSimple(string strTraditional)
        {
            string strSimple = Microsoft.VisualBasic.Strings.StrConv(strTraditional, VbStrConv.SimplifiedChinese, 1033);
            return strSimple;
        }
        #endregion
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            foreach (Control t in this.Controls)
            {
                if(t.Name.Contains("Box")|| t.Name.Contains("label"))
                {
                    if (config!=null &&config.ContainsKey(t.Name))
                        config[t.Name] = t.Text;
                    else
                        config.Add(t.Name,t.Text);
                }
            }
            File.WriteAllText("config.json", JsonConvert.SerializeObject(config));
            if(checkBox9.Checked)
                File.WriteAllText("data.json", JsonConvert.SerializeObject(json_data));
            if(checkBox1.Checked)
                File.WriteAllText("dict.json", JsonConvert.SerializeObject(dict_data));
            //彻底退出
            System.Environment.Exit(0);
        }
        /// <summary> 
        /// 通过给定的文件流，判断文件的编码类型 
        /// </summary> 
        /// <param name="fs">文件流</param> 
        /// <returns>文件的编码类型</returns> 
        public static Encoding GetEncoding(Stream fs)
        {
            byte[] Unicode = new byte[] { 0xFF, 0xFE, 0x41 };
            byte[] UnicodeBIG = new byte[] { 0xFE, 0xFF, 0x00 };
            byte[] UTF8 = new byte[] { 0xEF, 0xBB, 0xBF }; //带BOM 
            Encoding reVal = Encoding.Default;

            BinaryReader r = new BinaryReader(fs, System.Text.Encoding.Default);
            byte[] ss = r.ReadBytes(4);
            if (ss[0] == 0xFE && ss[1] == 0xFF && ss[2] == 0x00)
            {
                reVal = Encoding.BigEndianUnicode;
            }
            else if (ss[0] == 0xFF && ss[1] == 0xFE && ss[2] == 0x41)
            {
                reVal = Encoding.Unicode;
            }
            else
            {
                if (ss[0] == 0xEF && ss[1] == 0xBB && ss[2] == 0xBF)
                {
                    reVal = new UTF8Encoding(true);
                }
                else
                {
                    int i;
                    int.TryParse(fs.Length.ToString(), out i);
                    ss = r.ReadBytes(i);
                    if (IsUTF8Bytes(ss))
                        reVal = new UTF8Encoding(false);
                }
            }
            r.Close();
            return reVal;

        }

        /// <summary> 
        /// 判断是否是不带 BOM 的 UTF8 格式 
        /// </summary> 
        /// <param name="data"></param> 
        /// <returns></returns> 
        private static bool IsUTF8Bytes(byte[] data)
        {
            int charByteCounter = 1;  //计算当前正分析的字符应还有的字节数 
            byte curByte; //当前分析的字节. 
            for (int i = 0; i < data.Length; i++)
            {
                curByte = data[i];
                if (charByteCounter == 1)
                {
                    if (curByte >= 0x80)
                    {
                        //判断当前 
                        while (((curByte <<= 1) & 0x80) != 0)
                        {
                            charByteCounter++;
                        }
                        //标记位首位若为非0 则至少以2个1开始 如:110XXXXX...........1111110X　 
                        if (charByteCounter == 1 || charByteCounter > 6)
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    //若是UTF-8 此时第一位必须为1 
                    if ((curByte & 0xC0) != 0x80)
                    {
                        return false;
                    }
                    charByteCounter--;
                }
            }
            if (charByteCounter > 1)
            {
                throw new Exception("非预期的byte格式!");
            }
            return true;
        }

        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox8.Checked)
            {
                checkBox7.Checked = false;
            }
            else
            {
                checkBox7.Checked = true;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (button1.Text == "下一步")
            {
                button2.Enabled = false;
                button3.Enabled = false;
                press_button = 1;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (button1.Text == "下一步")
            {
                button2.Enabled = false;
                button3.Enabled = false;
                press_button = 2;
            }
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox11.Checked)
            {
                button2.Enabled = false;
                button3.Enabled = false;
                press_button = 999;
            }
            else
            {
                button2.Enabled = true;
                button3.Enabled = true;
                press_button = 0;
            }
        }

        private string[] spiltchar(string str,char a)
        {
            string[] res = new string[] { };
            if (str.Contains(a))
                res = str.Split(a);
            else if (str.Trim().Length > 0)
                res = new string[] { str };
            return res;
        }
        private Dictionary<string, string> read_dict(string path)
        {
            Dictionary<string, string> res = new Dictionary<string, string>();
            if (File.Exists(path))
            {
                try
                {
                    string JSONstring = File.ReadAllText(path);
                    var re = JsonConvert.DeserializeObject<Dictionary<string, string>>(JSONstring);
                    if (re != null)
                        res = re;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(path+"无法读取字典,或字典格式不规范！");
                }
            }
            return res;
        }
        private FileInfo[] return_all_files(string path)
        {
            FileInfo[] Filelist = new FileInfo[] { };
            try
            {
                if (Directory.Exists(path))
                    Filelist = GetFile(path, Filelist);
                else if(File.Exists(path))
                    Filelist = new FileInfo[] { new FileInfo(path) };
            }
            catch (Exception ex)
            {
                MessageBox.Show("源文件(夹)路径错误！");
            }
            return Filelist;
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (th == null)
                return;
            else
                stop = true;
            button1.Text = "开始";
            button1.Enabled = true;
            progressBar1.Value = 0;
            label12.Text = "进度:0/0";
            nowla.Text = "状态:";
            ori_text.Text = "";
            ana_text.Text = "";
            tran_text.Text = "";
            res_text.Text = "";
            Application.DoEvents();
        }
    }
}