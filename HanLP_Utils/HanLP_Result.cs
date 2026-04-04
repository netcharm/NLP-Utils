using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using com.hankcs.hanlp.seg.common;

namespace HanLP_Utils
{
    internal class HanLP_Result
    {
        internal List<Term> segments = new List<Term>();
        internal List<Term> tokenizer = new List<Term>();
        internal string keyword = string.Empty;
        internal string summary = string.Empty;
        internal string phrase = string.Empty;
        internal List<KeyValuePair<Term, int>> freq = new List<KeyValuePair<Term, int>>();
        internal string sc2tc = string.Empty;
        internal string tc2sc = string.Empty;
        internal string pinyin = string.Empty;
        internal string pinyinT = string.Empty;
        internal string pinyinM = string.Empty;
    }
}
