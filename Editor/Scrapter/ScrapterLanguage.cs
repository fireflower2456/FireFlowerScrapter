using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFlower
{
    // One editor language. The asset name is the language name shown in the picker.
    // Block titles use {field} for sockets, so word order can change: "wait {seconds} seconds" → "{seconds}초 기다리기"
    [CreateAssetMenu(menuName = "FireFlower/Scrapter Language", fileName = "Language")]
    public sealed class ScrapterLanguage : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string english;
            public string text;
        }

        public List<Entry> entries = new();

        void OnValidate() => ScrapterText.Refresh();
    }
}
