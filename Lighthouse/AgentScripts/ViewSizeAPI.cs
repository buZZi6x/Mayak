using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
public static class ViewSizeAPI { public static object Main(){ var a=typeof(EditorWindow).Assembly; return new[]{"UnityEditor.GameViewSizes","UnityEditor.GameViewSize","UnityEditor.GameView"}.Select(n=>new{name=n,constructors=a.GetType(n).GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).Select(x=>x.ToString()).ToArray(), members=a.GetType(n).GetMembers(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Where(x=>x.Name.Contains("Group")||x.Name.Contains("Size")||x.Name=="instance").Select(x=>x.ToString()).ToArray()}).ToArray();} }
