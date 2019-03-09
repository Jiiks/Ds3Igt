using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.TimeFormatters;
using LiveSplit.UI;
using LiveSplit.UI.Components;

namespace Ds3Igt
{
    public class Ds3Component : IComponent {

        private Ds3Control _control;

        private InfoTimeComponent _infoTimeComponent;
        private RegularTimeFormatter _timeFormatter;
        private Ds3Pointer Pointer;
        private Ds3AutoSplitter _splitter;

        public Ds3Component(LiveSplitState state) {
            _control = new Ds3Control();
            _timeFormatter = new RegularTimeFormatter(TimeAccuracy.Hundredths);
            _infoTimeComponent = new InfoTimeComponent(ComponentName, TimeSpan.Zero, _timeFormatter);
            _infoTimeComponent = new InfoTimeComponent(ComponentName, TimeSpan.Zero, _timeFormatter);
            
            state.OnReset += delegate {
                _oldMillis = 0;
            };

            state.OnStart += delegate {
                _oldMillis = 0;
            };

            Pointer = new Ds3Pointer();

            _splitter = new Ds3AutoSplitter(state, _control.splitSettings);
        }

        public void Dispose() => _infoTimeComponent.Dispose();

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion) { }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion) { }

        public Control GetSettingsControl(LayoutMode mode) => _control ?? (_control = new Ds3Control());

        public XmlNode GetSettings(XmlDocument document) => _control.GetSettings(document);

        public void SetSettings(XmlNode settings) => _control.SetSettings(settings);

        public string ComponentName => "Dark Souls 3 In-Game Time Component";
        public float HorizontalWidth => 0;
        public float MinimumHeight => 0;
        public float VerticalHeight => 0;
        public float MinimumWidth => 0;
        public float PaddingTop => 0;
        public float PaddingBottom => 0;
        public float PaddingLeft => 0;
        public float PaddingRight => 0;
        public IDictionary<string, Action> ContextMenuControls => null;

        private int _oldMillis;
        private bool _latch;

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode) {

            int millis = Pointer.GetIgt(); //Native.GetGameTimeMilliseconds(addr, p.Id.GetHandle(), 8);
            if (millis > 100) {
                _oldMillis = millis;
                _latch = false;
            }

            if (millis == 0 && !_latch) {
                _oldMillis -= 594;
                _latch = true;
            }
            if (_oldMillis <= 0) _oldMillis = 0;

            state.IsGameTimePaused = true;
            state.SetGameTime(new TimeSpan(0, 0, 0, 0, _oldMillis <= 1 ? 1 : _oldMillis));

            //autostart timer. Might be worth changing this to something based on some memory flag
            if (_control.cb_autoStartTimer.Checked && millis > 0 && millis < 500)
            {
                if (state.CurrentPhase == TimerPhase.NotRunning)
                {
                    TimerModel timer = new TimerModel();
                    timer.CurrentState = state;
                    timer.Start();
                }
            }

            //autosplit
            if (_control.cb_autoSplit.Checked)
                _splitter.AttemptSplit();
        }
    }
}
