using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
public static class ResizeKeeperUI
{
 public static object Main(int width,int height){
  const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
  var a=typeof(EditorWindow).Assembly; var t=a.GetType("UnityEditor.GameViewSizes");
  var singleton=typeof(ScriptableSingleton<>).MakeGenericType(t);
  var sizes=singleton.GetProperty("instance",f).GetValue(null);
  var group=t.GetProperty("currentGroup",f).GetValue(sizes); var gt=group.GetType();
  int count=(int)gt.GetMethod("GetTotalCount",f).Invoke(group,null); int index=-1;
  for(int i=0;i<count;i++){var size=gt.GetMethod("GetGameViewSize",f).Invoke(group,new object[]{i}); var st=size.GetType(); if((int)st.GetProperty("width",f).GetValue(size)==width&&(int)st.GetProperty("height",f).GetValue(size)==height){index=i;break;}}
  if(index<0){var size=Activator.CreateInstance(a.GetType("UnityEditor.GameViewSize"),f,null,new object[]{Enum.Parse(a.GetType("UnityEditor.GameViewSizeType"),"FixedResolution"),width,height,"Keeper UI "+width+"x"+height},null);gt.GetMethod("AddCustomSize",f).Invoke(group,new[]{size});index=count;}
  var view=EditorWindow.GetWindow(a.GetType("UnityEditor.GameView"));
  view.GetType().GetProperty("selectedSizeIndex",f).SetValue(view,index); view.Repaint();
  return new{width,height,index};
 }
}
