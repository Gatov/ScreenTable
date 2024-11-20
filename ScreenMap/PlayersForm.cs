using System.Windows.Forms;
using ScreenMap.Controls;
using ScreenMap.Logic;

namespace ScreenMap
{
    public partial class PlayersForm : DevExpress.XtraEditors.XtraForm
    {
        private readonly PlayerController _playerController;
        private readonly PlayersMapView _plyersView;

        public PlayersForm(PlayerController playerController)
        {
            
            _playerController = playerController;
            InitializeComponent();
            _plyersView = new PlayersMapView();
            _plyersView.Dock = DockStyle.Fill;
            Controls.Add(_plyersView);
            
            playerController.SetView(_plyersView);
        }
    }
}