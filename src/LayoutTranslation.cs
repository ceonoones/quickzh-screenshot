using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace QuickZh {
    public sealed class BlockReply { public int id { get; set; } public string text { get; set; } }
    public sealed class LayoutReply { public List<BlockReply> blocks { get; set; } }
    public static partial class ApiClient {
        private static readonly Regex CompleteBlock = new Regex(@"\{\s*""id""\s*:\s*(\d+)\s*,\s*""text""\s*:\s*(""(?:[^""\\]|\\.)*"")\s*\}",RegexOptions.Compiled);
        public static LayoutReply ParseLayout(string raw,int count) {
            string clean = raw.Trim();
            if (clean.StartsWith("```")) { int first = clean.IndexOf('\n'),last = clean.LastIndexOf("```",StringComparison.Ordinal); if (first>=0 && last>first) clean = clean.Substring(first+1,last-first-1).Trim(); }
            LayoutReply reply = null;
            try { reply = Json.Parse<LayoutReply>(clean); } catch { }
            if (reply == null || reply.blocks == null) {
                if (count == 1 && !clean.StartsWith("{") && !String.IsNullOrWhiteSpace(clean)) return new LayoutReply { blocks = new List<BlockReply> {new BlockReply { id = 0,text = clean }} };
                throw new InvalidOperationException("模型未按区域返回译文，请按 Ctrl+R 重试，或更换模型。");
            }
            var ids = new HashSet<int>();
            foreach (var b in reply.blocks) if (b.id<0 || b.id>=count || !ids.Add(b.id) || String.IsNullOrWhiteSpace(b.text)) throw new InvalidOperationException("模型返回的段落不完整，请重新翻译。");
            if (ids.Count != count) throw new InvalidOperationException("模型遗漏了部分段落，请重新翻译。");
            reply.blocks.Sort((a,b)=>a.id.CompareTo(b.id)); return reply;
        }
        public static async Task<LayoutReply> TranslateLayout(Settings settings,List<OcrBlock> blocks,Action<int,string> onBlock,CancellationToken token) {
            var s = settings.Clone();
            s.Prompt += "\n本次输入是带有段落编号的 JSON。请结合全部段落理解上下文，然后分别翻译每个段落，用于在截图原位置替换文字。必须保留所有 id，一一对应，不要合并、拆分或遗漏段落。响应格式必须为 JSON 对象：{\"blocks\":[{\"id\":0,\"text\":\"中文译文\"}]}。每个对象先写 id 再写 text。不要 Markdown，不要解释，JSON 中的 text 只包含译文。";
            s.Prompt += "\n每个 id 是独立文字区域，可能是一行文字或一个界面标签。结合相邻区域理解句意，但译文必须留在各自区域内。界面菜单、按钮使用简洁自然的中文，避免冗长解释；不要把界面图标或装饰符号写进译文，不要添加原文没有的箭头、勾选符号等。品牌名、网址、型号可按语境保留。";
            var input = new List<BlockReply>(); foreach (var block in blocks) input.Add(new BlockReply { id = block.Id,text = block.Text });
            var pending = new StringBuilder(); var delivered = new HashSet<int>();
            string raw = await Translate(s,Json.Stringify(new LayoutReply {blocks=input}),delta=> {
                pending.Append(delta);
                foreach (Match match in CompleteBlock.Matches(pending.ToString())) {
                    int id = Int32.Parse(match.Groups[1].Value);
                    if (id>=0 && id<blocks.Count && delivered.Add(id)) {
                        string text = Json.Parse<string>(match.Groups[2].Value); if (onBlock != null) onBlock(id,text);
                    }
                }
            },token);
            return ParseLayout(raw,blocks.Count);
        }
    }
}
