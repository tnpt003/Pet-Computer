using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DesktopPet;

// The pet's right-click menu (PetUi palette): rounded white panel with a soft shadow, rounded hover rows,
// a fixed icon column (a check mark when a checked item has no icon), submenu arrows and
// dimmed disabled items. Applied to the menu and, through its resources, every item and
// submenu. The shadow sits on its own layer so menu text keeps crisp ClearType.
public static class PetMenuStyle
{
    private const string Xaml = $$$"""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="PetMenuLine" Color="{{{PetUi.LineHex}}}"/>
  <DropShadowEffect x:Key="PetMenuShadow" BlurRadius="14" ShadowDepth="3" Direction="270" Opacity="0.18"/>

  <Style x:Key="{x:Static MenuItem.SeparatorStyleKey}" TargetType="Separator">
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Separator">
          <Border Height="1" Margin="10,4,10,4" Background="{StaticResource PetMenuLine}"/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="MenuItem">
    <Setter Property="Foreground" Value="{{{PetUi.InkHex}}}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="MenuItem">
          <Grid SnapsToDevicePixels="True">
            <Border x:Name="Chrome" Margin="0,1,0,1" CornerRadius="8" Background="Transparent" Padding="0,5,10,5" MinHeight="30">
              <Grid>
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="34"/>
                  <ColumnDefinition Width="*"/>
                  <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <Border x:Name="IconBack" Width="24" Height="24" CornerRadius="6" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                <ContentPresenter ContentSource="Icon" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                <TextBlock x:Name="Check" Text="&#xE73E;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="12"
                           Foreground="{{{PetUi.AccentHex}}}" HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed"/>
                <ContentPresenter Grid.Column="1" ContentSource="Header" RecognizesAccessKey="True" VerticalAlignment="Center" Margin="2,0,14,0"/>
                <TextBlock x:Name="Arrow" Grid.Column="2" Text="&#xE76C;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="10"
                           Foreground="{{{PetUi.MutedHex}}}" VerticalAlignment="Center" Visibility="Collapsed"/>
              </Grid>
            </Border>
            <Popup x:Name="PART_Popup" Placement="Right" HorizontalOffset="-2" VerticalOffset="-7" AllowsTransparency="True"
                   Focusable="False" PopupAnimation="Fade" IsOpen="{Binding IsSubmenuOpen, RelativeSource={RelativeSource TemplatedParent}}">
              <Grid Margin="4,2,12,12">
                <Border CornerRadius="12" Background="White" Effect="{StaticResource PetMenuShadow}"/>
                <Border CornerRadius="12" Background="White" BorderBrush="{StaticResource PetMenuLine}" BorderThickness="1" Padding="4">
                  <ScrollViewer VerticalScrollBarVisibility="Auto" CanContentScroll="True">
                    <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Cycle" KeyboardNavigation.TabNavigation="Cycle"/>
                  </ScrollViewer>
                </Border>
              </Grid>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="Role" Value="SubmenuHeader">
              <Setter TargetName="Arrow" Property="Visibility" Value="Visible"/>
            </Trigger>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="Chrome" Property="Background" Value="{{{PetUi.AccentSoftHex}}}"/>
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="IconBack" Property="Background" Value="{{{PetUi.AccentSofterHex}}}"/>
            </Trigger>
            <MultiTrigger>
              <MultiTrigger.Conditions>
                <Condition Property="IsChecked" Value="True"/>
                <Condition Property="Icon" Value="{x:Null}"/>
              </MultiTrigger.Conditions>
              <Setter TargetName="Check" Property="Visibility" Value="Visible"/>
            </MultiTrigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter TargetName="Chrome" Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key="PetContextMenu" TargetType="ContextMenu">
    <Setter Property="HasDropShadow" Value="False"/>
    <Setter Property="Foreground" Value="{{{PetUi.InkHex}}}"/>
    <Setter Property="MinWidth" Value="200"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ContextMenu">
          <Grid Margin="4,2,12,12">
            <Border CornerRadius="12" Background="White" Effect="{StaticResource PetMenuShadow}"/>
            <Border CornerRadius="12" Background="White" BorderBrush="{StaticResource PetMenuLine}" BorderThickness="1" Padding="4">
              <ScrollViewer VerticalScrollBarVisibility="Auto" CanContentScroll="True">
                <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Cycle" KeyboardNavigation.TabNavigation="Cycle"/>
              </ScrollViewer>
            </Border>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
""";

    private static ResourceDictionary? resources;

    public static void Apply(ContextMenu menu)
    {
        resources ??= (ResourceDictionary)XamlReader.Parse(Xaml);
        menu.Resources.MergedDictionaries.Add(resources);
        menu.Style = (Style)resources["PetContextMenu"];
    }
}
