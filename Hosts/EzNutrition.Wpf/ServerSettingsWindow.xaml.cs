using EzNutrition.Wpf.Configuration;
using EzNutrition.Presentation.Services;
using EzNutrition.Wpf.Security;
using System.Windows;

namespace EzNutrition.Wpf;

/// <summary>
/// 提供仅属于 Windows 桌面宿主的服务连接与本机凭据设置。
/// </summary>
internal partial class ServerSettingsWindow : Window
{
    private readonly DpapiLoginCredentialStore credentialStore;
    private readonly WpfUserSettingsStore settingsStore;
    private readonly UserSessionService userSession;

    /// <summary>创建服务连接设置窗口。</summary>
    internal ServerSettingsWindow(
        WpfUserSettingsStore settingsStore,
        DpapiLoginCredentialStore credentialStore,
        UserSessionService userSession)
    {
        this.settingsStore = settingsStore ??
            throw new ArgumentNullException(nameof(settingsStore));
        this.credentialStore = credentialStore ??
            throw new ArgumentNullException(nameof(credentialStore));
        this.userSession = userSession ?? throw new ArgumentNullException(nameof(userSession));

        InitializeComponent();
        ServerAddressTextBox.Text =
            settingsStore.ConfiguredServerBaseAddress.AbsoluteUri;
        SelectTransportSecurity(settingsStore.ConfiguredTransportSecurity);
        UpdateRiskPanel(resetAcknowledgement: false);
        UpdateCredentialStatus();
    }

    private ServerTransportSecurity SelectedTransportSecurity =>
        SelfSignedHttpsRadio.IsChecked == true
            ? ServerTransportSecurity.AllowSelfSignedHttps
            : InsecureHttpRadio.IsChecked == true
                ? ServerTransportSecurity.InsecureHttp
                : ServerTransportSecurity.StrictHttps;

    private void SelectTransportSecurity(ServerTransportSecurity transportSecurity)
    {
        switch (transportSecurity)
        {
            case ServerTransportSecurity.StrictHttps:
                StrictHttpsRadio.IsChecked = true;
                break;
            case ServerTransportSecurity.AllowSelfSignedHttps:
                SelfSignedHttpsRadio.IsChecked = true;
                break;
            case ServerTransportSecurity.InsecureHttp:
                InsecureHttpRadio.IsChecked = true;
                break;
            default:
                throw new InvalidOperationException("遇到不受支持的传输安全策略。");
        }
    }

    private void TransportSecurityChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateRiskPanel(resetAcknowledgement: true);
        }
    }

    private void RiskAcknowledgementChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateSaveAvailability();
        }
    }

    private void UpdateRiskPanel(bool resetAcknowledgement)
    {
        var transportSecurity = SelectedTransportSecurity;
        var hasElevatedRisk = transportSecurity != ServerTransportSecurity.StrictHttps;
        RiskPanel.Visibility = hasElevatedRisk ? Visibility.Visible : Visibility.Collapsed;
        if (resetAcknowledgement)
        {
            RiskAcknowledgement.IsChecked = false;
        }

        RiskWarningText.Text = transportSecurity switch
        {
            ServerTransportSecurity.AllowSelfSignedHttps =>
                "风险警示：自签名证书没有公共信任链。桌面宿主会继续拒绝域名不符、过期或并非自签名的证书，但攻击者仍可能伪造同名自签名证书。请通过独立渠道核对机构端点。",
            ServerTransportSecurity.InsecureHttp =>
                "严重风险：HTTP 完全不加密。网络中的其他设备可能读取或篡改用户名、密码、访问令牌、参考数据请求及 AI 业务内容。只应在受控且隔离的网络中临时使用。",
            _ => string.Empty
        };
        UpdateSaveAvailability();
    }

    private void UpdateSaveAvailability()
    {
        SaveButton.IsEnabled =
            SelectedTransportSecurity == ServerTransportSecurity.StrictHttps ||
            RiskAcknowledgement.IsChecked == true;
    }

    private async void SaveSettings(object sender, RoutedEventArgs e)
    {
        ValidationMessage.Text = string.Empty;
        var transportSecurity = SelectedTransportSecurity;
        if (transportSecurity != ServerTransportSecurity.StrictHttps &&
            RiskAcknowledgement.IsChecked != true)
        {
            ValidationMessage.Text = "必须明确确认红色风险警示后才能保存该连接策略。";
            return;
        }

        Uri serverBaseAddress;
        try
        {
            serverBaseAddress = WpfHostSettings.ValidateServerConnection(
                ServerAddressTextBox.Text,
                transportSecurity);
        }
        catch (InvalidOperationException exception)
        {
            ValidationMessage.Text = exception.Message;
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            await settingsStore.SaveAsync(serverBaseAddress, transportSecurity);
            MessageBox.Show(
                this,
                "服务连接设置已经安全写入。请重启 EzNutrition 以使用新设置。",
                "设置已保存",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            ValidationMessage.Text = $"无法保存用户设置：{exception.Message}";
            UpdateSaveAvailability();
        }
    }

    private async void ClearSavedCredential(object sender, RoutedEventArgs e)
    {
        if (!credentialStore.HasSavedCredential)
        {
            UpdateCredentialStatus();
            return;
        }

        var answer = MessageBox.Show(
            this,
            "确定退出当前账号，并清除当前连接保存的登录信息吗？",
            "清除登录信息",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await userSession.SignOutAsync();
            ValidationMessage.Text = userSession.CredentialPersistenceWarning ?? string.Empty;
            UpdateCredentialStatus();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            ValidationMessage.Text = $"无法清除保存的登录信息：{exception.Message}";
        }
    }

    private void UpdateCredentialStatus()
    {
        CredentialStatusText.Text = credentialStore.HasSavedCredential
            ? "当前生效的“端点 + 安全策略”已有一份由 Windows 当前用户保护的登录信息。"
            : "当前生效的“端点 + 安全策略”没有保存登录信息。";
    }
}
