using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    public sealed partial class KeeperMenu
    {
        private sealed class Choice
        {
            internal Button Button;
            internal Label Value;
            internal string[] Options;
            internal int Selected;
            internal Action<int> Changed;
        }
        private readonly Dictionary<string, OpticalSlider> _sliders = new Dictionary<string, OpticalSlider>();
        private readonly Dictionary<string, Label> _values = new Dictionary<string, Label>();
        private readonly Dictionary<string, Toggle> _toggles = new Dictionary<string, Toggle>();
        private readonly Dictionary<string, Choice> _choices = new Dictionary<string, Choice>();
        private Choice _openChoice;
        private int _choiceHighlight;
        private bool _displayPending;
        private float _displayDeadline;
        private int _previousWidth, _previousHeight;
        private FullScreenMode _previousMode;
        private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

        private void BuildSettings()
        {
            var audio = _root.Q("panel-audio"); var video = _root.Q("panel-video"); var input = _root.Q("panel-controls");
            AddSlider(audio,"master","Общая громкость",0,100,value=>_draft.Master=(int)value);
            AddSlider(audio,"ambience","Окружение",0,100,value=>_draft.Ambience=(int)value);
            AddSlider(audio,"effects","Эффекты",0,100,value=>_draft.Effects=(int)value);
            var test = new Button(() => { _testAudio.volume = _draft.Effects/100f*.8f; _testAudio.Play(); }) { name="test-audio", text="Проверить звук" };
            test.AddToClassList("audio-test"); audio.Add(test);
            AddChoice(video,"display","Режим экрана",new[] {"Без рамки","Полный экран","В окне"},i=>_draft.Display=i);
            AddChoice(video,"resolution","Разрешение",new[] {"1280 × 720","1920 × 1080","2560 × 1440","3840 × 2160"},i=>
            { _draft.Width=new[]{1280,1920,2560,3840}[i]; _draft.Height=new[]{720,1080,1440,2160}[i]; });
            AddToggle(video,"vsync","Вертикальная синхронизация",value=>_draft.VSync=value);
            AddChoice(video,"fps","Лимит кадров",new[] {"30 FPS","60 FPS","120 FPS","144 FPS","Без ограничения"},i=>_draft.FrameLimit=new[]{30,60,120,144,0}[i],"При включённой синхронизации — частота экрана");
            AddSlider(video,"brightness","Яркость",70,130,value=>_draft.Brightness=(int)value);
            AddToggle(video,"glitches","Сбои фона",value=>_draft.Glitches=value,"Редкие красные помехи в главном меню");
            AddSlider(input,"sensitivity","Чувствительность мыши",.2f,3,value=>_draft.Sensitivity=value);
            AddToggle(input,"invertY","Инверсия по вертикали",value=>_draft.InvertY=value);
            var keys = new VisualElement(); keys.AddToClassList("key-list"); input.Add(keys);
            string[,] bindings = { {"Движение","W  A  S  D"},{"Бег / прыжок","Shift  Пробел"},{"Фотографии в руке","1 — 5"},{"Личный фонарь","Q"},
                {"Достать / убрать камеру","E"},{"Взаимодействие","F"},{"Снимок / приближение","ЛКМ  ПКМ"},{"Скрыть / вернуть UI","Tab"} };
            for (int i=0;i<bindings.GetLength(0);i++)
            {
                var row=new VisualElement(); row.AddToClassList("key-row");
                var label=new Label(bindings[i,0]); label.AddToClassList("key-label");
                var key=new Label(bindings[i,1]); key.AddToClassList("key-value"); row.Add(label); row.Add(key); keys.Add(row);
            }
            string[] sections={"audio","video","controls"};
            for (int i=0;i<sections.Length;i++)
            {
                string section=sections[i]; int index=i;
                Bind("tab-"+section,()=>SelectTab(section));
                _root.Q<Button>("tab-"+section).RegisterCallback<KeyDownEvent>(evt=>
                {
                    int next=evt.keyCode==KeyCode.Home?0:evt.keyCode==KeyCode.End?2:evt.keyCode==KeyCode.LeftArrow?(index+2)%3:evt.keyCode==KeyCode.RightArrow?(index+1)%3:-1;
                    if(next<0)return; SelectTab(sections[next]); _root.Q<Button>("tab-"+sections[next]).Focus(); evt.StopPropagation();
                });
            }
            Bind("reset-settings",()=>{_draft=new MenuSettings();RenderSettings();PreviewSettings();});
            Bind("apply-settings",ApplySettings);
        }

        private VisualElement SettingRow(VisualElement parent,string label,string description=null)
        {
            var row=new VisualElement(); row.AddToClassList("setting-row");
            var column=new VisualElement(); column.AddToClassList("setting-label-column");
            var title=new Label(label); title.AddToClassList("setting-label"); column.Add(title);
            if(description!=null){var hint=new Label(description);hint.AddToClassList("setting-description");column.Add(hint);}
            row.Add(column);parent.Add(row);return row;
        }

        private void AddSlider(VisualElement panel,string name,string label,float min,float max,Action<float> changed)
        {
            var row=SettingRow(panel,label); var range=new VisualElement();range.AddToClassList("range-control");row.Add(range);
            var slider=new OpticalSlider{name=name,lowValue=min,highValue=max,tooltip=label};
            var output=new Label();output.AddToClassList("range-value"); range.Add(slider);range.Add(output);
            _sliders.Add(name,slider);_values.Add(name,output);
            slider.RegisterValueChangedCallback(evt=>{float value=name=="sensitivity"?Mathf.Round(evt.newValue*10)/10:Mathf.Round(evt.newValue);changed(value);RenderSettings();PreviewSettings();});
        }

        private void AddToggle(VisualElement panel,string name,string label,Action<bool> changed,string description=null)
        {
            var row=SettingRow(panel,label,description);var toggle=new Toggle{name=name,tooltip=label};
            var mark=new VisualElement();mark.AddToClassList("toggle-fill");mark.pickingMode=PickingMode.Ignore;
            toggle.Q(className:"unity-toggle__checkmark").Add(mark);
            toggle.AddToClassList("setting-toggle");row.Add(toggle);_toggles.Add(name,toggle);
            toggle.RegisterValueChangedCallback(evt=>{changed(evt.newValue);RenderSettings();PreviewSettings();});
        }

        private void AddChoice(VisualElement panel,string name,string label,string[] options,Action<int> changed,string description=null)
        {
            var row=SettingRow(panel,label,description);var choice=new Choice{Options=options,Changed=changed};
            choice.Button=new Button(()=>ToggleChoice(choice)){name=name,tooltip=label};choice.Button.AddToClassList("choice-field");
            choice.Value=new Label();choice.Button.Add(choice.Value);var arrow=new VisualElement{pickingMode=PickingMode.Ignore};arrow.AddToClassList("choice-arrow");choice.Button.Add(arrow);
            row.Add(choice.Button);_choices.Add(name,choice);
            choice.Button.RegisterCallback<KeyDownEvent>(evt=>
            {
                bool isArrow=evt.keyCode==KeyCode.DownArrow||evt.keyCode==KeyCode.UpArrow;
                if(evt.keyCode==KeyCode.Escape&&_openChoice==choice){CloseChoice();evt.StopPropagation();return;}
                if(isArrow||evt.keyCode==KeyCode.Home||evt.keyCode==KeyCode.End)
                {
                    if(_openChoice!=choice)OpenChoice(choice);
                    else _choiceHighlight=evt.keyCode==KeyCode.Home?0:evt.keyCode==KeyCode.End?choice.Options.Length-1:(_choiceHighlight+(evt.keyCode==KeyCode.DownArrow?1:choice.Options.Length-1))%choice.Options.Length;
                    HighlightChoice();evt.StopPropagation();_root.panel.focusController.IgnoreEvent(evt);
                }
            });
            choice.Button.RegisterCallback<NavigationSubmitEvent>(evt=>
            { if(_openChoice==choice)Choose(_choiceHighlight);else OpenChoice(choice);evt.StopImmediatePropagation(); },TrickleDown.TrickleDown);
        }

        private void ToggleChoice(Choice choice){if(_openChoice==choice)CloseChoice();else OpenChoice(choice);}
        private void OpenChoice(Choice choice)
        {
            CloseChoice();_openChoice=choice;_choiceHighlight=choice.Selected;
            var popup=_root.Q("choice-popup");popup.Clear();
            for(int i=0;i<choice.Options.Length;i++)
            {
                int index=i;var option=new Button(()=>Choose(index)){text=choice.Options[i],tabIndex=-1};option.AddToClassList("choice-option");
                option.RegisterCallback<PointerEnterEvent>(_=>{_choiceHighlight=index;HighlightChoice();});popup.Add(option);
            }
            Vector2 position=_root.WorldToLocal(choice.Button.worldBound.position);
            float height=choice.Options.Length*42+14;
            popup.style.left=position.x;popup.style.width=choice.Button.resolvedStyle.width;
            popup.style.top=position.y+choice.Button.resolvedStyle.height+height+10>_root.resolvedStyle.height&&position.y>height?position.y-height-3:position.y+choice.Button.resolvedStyle.height+3;
            choice.Button.AddToClassList("is-open");Show("choice-popup",true);HighlightChoice();
        }
        private void HighlightChoice(){var list=_root.Q("choice-popup");for(int i=0;i<list.childCount;i++)list[i].EnableInClassList("selected",i==_choiceHighlight);}
        private void Choose(int index)
        {
            var choice=_openChoice;if(choice==null)return;choice.Changed(index);CloseChoice();RenderSettings();PreviewSettings();choice.Button.Focus();
        }
        private void CloseChoice(){if(_root==null)return;Show("choice-popup",false);_openChoice?.Button.RemoveFromClassList("is-open");_openChoice=null;}

        public void SelectTab(string section)
        {
            CloseChoice();_section=section;
            foreach(var name in new[]{"audio","video","controls"})
            {Show("panel-"+name,name==section);var button=_root.Q<Button>("tab-"+name);button.EnableInClassList("active",name==section);button.tabIndex=name==section?0:-1;}
            _root.Q("settings-form").EnableInClassList("dense",section!="audio");
            _root.Q<Button>("tab-"+section).Focus();
        }

        private void RenderSettings()
        {
            _draft.Normalize();
            SetSlider("master",_draft.Master);SetSlider("ambience",_draft.Ambience);SetSlider("effects",_draft.Effects);
            SetSlider("brightness",_draft.Brightness);SetSlider("sensitivity",_draft.Sensitivity);
            SetToggle("vsync",_draft.VSync);SetToggle("glitches",_draft.Glitches);SetToggle("invertY",_draft.InvertY);
            SetChoice("display",_draft.Display);SetChoice("resolution",Array.IndexOf(new[]{1280,1920,2560,3840},_draft.Width));
            SetChoice("fps",Array.IndexOf(new[]{30,60,120,144,0},_draft.FrameLimit));_choices["fps"].Button.SetEnabled(!_draft.VSync);
            if(_draft.VSync&&_openChoice==_choices["fps"])CloseChoice();
            bool dirty=!_draft.SameAs(_saved);_root.Q<Button>("apply-settings").SetEnabled(dirty);
            Text("settings-status",dirty?"Есть неприменённые изменения":"");
        }
        private void SetSlider(string name,float value){_sliders[name].SetValueWithoutNotify(value);_sliders[name].MarkDirtyRepaint();_values[name].text=name=="sensitivity"?value.ToString("0.0",Russian)+"×":value.ToString("0",Russian)+"%";}
        private void SetToggle(string name,bool value){_toggles[name].SetValueWithoutNotify(value);_toggles[name].text=value?"Вкл.":"Выкл.";}
        private void SetChoice(string name,int value){var choice=_choices[name];choice.Selected=Mathf.Clamp(value,0,choice.Options.Length-1);choice.Value.text=choice.Options[choice.Selected];}
        private void PreviewSettings(){ApplyAudio(_draft);_backdrop.Brightness=_draft.Brightness/100f;_backdrop.Glitches=_draft.Glitches;}

        private void ApplyAudio(MenuSettings settings)
        {
            AudioListener.volume=settings.Master/100f;
            if(_music)_music.volume=settings.Ambience/100f*.35f;
            var ambience=CinematicEnvironment.Active?CinematicEnvironment.Active.Ambience:null;
            if(ambience){ambience.UserVolume=settings.Ambience/100f;ambience.MenuMultiplier=.12f;ambience.EffectsVolume=0;}
        }
        private void ApplyRuntime(MenuSettings settings)
        {
            ApplyAudio(settings);QualitySettings.vSyncCount=settings.VSync?1:0;Application.targetFrameRate=settings.FrameLimit==0?-1:settings.FrameLimit;
            if(_backdrop){_backdrop.Brightness=settings.Brightness/100f;_backdrop.Glitches=settings.Glitches;}
        }
        private void ApplySettings()
        {
            if(_draft.SameAs(_saved))return;
            if(!_draft.SameDisplay(_saved))
            {
                _previousWidth=Screen.width;_previousHeight=Screen.height;_previousMode=Screen.fullScreenMode;
                _displayPending=true;_displayDeadline=Time.unscaledTime+15;
                if(!Application.isEditor)Screen.SetResolution(_draft.Width,_draft.Height,DisplayMode(_draft.Display));
                Dialog("Сохранить режим экрана?","","Вернуть","Сохранить",CancelDisplayChange,SaveSettings);
                TickDisplayConfirmation();return;
            }
            SaveSettings();
        }
        private static FullScreenMode DisplayMode(int display)=>display==0?FullScreenMode.FullScreenWindow:display==1?FullScreenMode.ExclusiveFullScreen:FullScreenMode.Windowed;
        private void SaveSettings()
        {
            if(!_draft.Save(_settingsPath,out string error)){CancelDisplayChange();Text("settings-status",error);return;}
            _displayPending=false;_saved=_draft.Clone();ApplyRuntime(_saved);HideDialog();RenderSettings();Text("settings-status","Настройки сохранены");
        }
        private void CancelDisplayChange()
        {
            if(!_displayPending)return;
            _displayPending=false;
            if(!Application.isEditor)Screen.SetResolution(_previousWidth,_previousHeight,_previousMode);
            _draft=_saved.Clone();ApplyRuntime(_saved);HideDialog();RenderSettings();
        }
        private void TickDisplayConfirmation()
        {
            if(!_displayPending)return;
            int seconds=Mathf.CeilToInt(_displayDeadline-Time.unscaledTime);
            if(seconds<=0){CancelDisplayChange();return;}
            Text("dialog-copy","Автоматический возврат через "+seconds+" с.");
        }
    }
}
