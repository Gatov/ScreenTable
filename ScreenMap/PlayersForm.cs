using System.Drawing;
using System.Windows.Forms;
using DevExpress.Utils.Extensions;
using ScreenMap.Controls;
using ScreenMap.Logic;

namespace ScreenMap
{
    public partial class PlayersForm : DevExpress.XtraEditors.XtraForm
    {
        private readonly PlayerController _playerController;
        private readonly PlayersMapView _plyersView;
        private bool _fullscreen = false;
        private Rectangle _savedLocation;

        public PlayersForm(PlayerController playerController)
        {
            
            _playerController = playerController;
            InitializeComponent();
            _plyersView = new PlayersMapView();
            _plyersView.Dock = DockStyle.Fill;
            Controls.Add(_plyersView);
            
            playerController.SetView(_plyersView);
            this.KeyPreview = true;
            KeyDown+= OnKeyDown;
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Alt == true && e.KeyCode == Keys.Enter)
            {
                _fullscreen = !_fullscreen;
                if (_fullscreen)
                    _savedLocation = this.Bounds;
                this.SetWindowStateFast(_fullscreen?FormWindowState.Maximized:FormWindowState.Normal);
                this.FormBorderStyle = _fullscreen ? FormBorderStyle.None : FormBorderStyle.Sizable;
                e.SuppressKeyPress = true;
                if (!_fullscreen)
                    this.SetBounds(_savedLocation);

            }
        }

    }
}