using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Markup;

namespace DesktopPet;

// Warm (PetUi palette) looks for the standard controls in the pet's own windows: switches for
// check boxes, rounded text boxes and drop-downs, an orange slider, thin scroll bars and a
// sidebar for tabs (a TabItem's Tag holds its Segoe icon glyph). Implicit styles, so each
// window only merges them; the controls keep their types, events and keyboard behavior.
public static class PetControlStyle
{
    private const string Xaml = $$$"""
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="PetInk" Color="{{{PetUi.InkHex}}}"/>
  <SolidColorBrush x:Key="PetMuted" Color="{{{PetUi.MutedHex}}}"/>
  <SolidColorBrush x:Key="PetAccent" Color="{{{PetUi.AccentHex}}}"/>
  <SolidColorBrush x:Key="PetAccentBright" Color="{{{PetUi.AccentBrightHex}}}"/>
  <SolidColorBrush x:Key="PetAccentSoft" Color="{{{PetUi.AccentSoftHex}}}"/>
  <SolidColorBrush x:Key="PetAccentSofter" Color="{{{PetUi.AccentSofterHex}}}"/>
  <SolidColorBrush x:Key="PetLine" Color="{{{PetUi.LineHex}}}"/>
  <SolidColorBrush x:Key="PetControlLine" Color="{{{PetUi.ControlLineHex}}}"/>
  <SolidColorBrush x:Key="PetControlEdge" Color="{{{PetUi.ControlEdgeHex}}}"/>
  <SolidColorBrush x:Key="PetStage" Color="{{{PetUi.StageHex}}}"/>
  <SolidColorBrush x:Key="PetTrackOff" Color="#EADBCB"/>
  <DropShadowEffect x:Key="PetPopupShadow" BlurRadius="12" ShadowDepth="3" Direction="270" Opacity="0.16"/>

  <Style x:Key="PetFocus">
    <Setter Property="Control.Template">
      <Setter.Value>
        <ControlTemplate>
          <Border Margin="-3" CornerRadius="10" BorderBrush="{StaticResource PetAccent}" BorderThickness="2" SnapsToDevicePixels="True"/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Check box as a switch: track and knob on the left, label on the right. -->
  <Style TargetType="CheckBox">
    <Setter Property="Foreground" Value="{StaticResource PetInk}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="CheckBox">
          <Grid Background="Transparent" SnapsToDevicePixels="True">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="Auto"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Border x:Name="Track" Width="36" Height="20" CornerRadius="10" VerticalAlignment="Center"
                    Background="{StaticResource PetTrackOff}" BorderBrush="{StaticResource PetControlEdge}" BorderThickness="1">
              <Ellipse x:Name="Knob" Width="14" Height="14" Fill="White" HorizontalAlignment="Left" Margin="2,0,0,0">
                <Ellipse.Effect>
                  <DropShadowEffect BlurRadius="3" ShadowDepth="1" Direction="270" Opacity="0.25"/>
                </Ellipse.Effect>
              </Ellipse>
            </Border>
            <ContentPresenter Grid.Column="1" Margin="10,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Track" Property="BorderBrush" Value="{StaticResource PetAccentBright}"/>
            </Trigger>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Track" Property="Background" Value="{StaticResource PetAccent}"/>
              <Setter TargetName="Track" Property="BorderBrush" Value="{StaticResource PetAccent}"/>
              <Setter TargetName="Knob" Property="HorizontalAlignment" Value="Right"/>
              <Setter TargetName="Knob" Property="Margin" Value="0,0,2,0"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Track" Property="BorderBrush" Value="{StaticResource PetInk}"/>
              <Setter TargetName="Track" Property="BorderThickness" Value="2"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="TextBox">
    <Setter Property="Foreground" Value="{StaticResource PetInk}"/>
    <Setter Property="Background" Value="White"/>
    <Setter Property="BorderBrush" Value="{StaticResource PetControlEdge}"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="6,4,6,4"/>
    <Setter Property="CaretBrush" Value="{StaticResource PetInk}"/>
    <Setter Property="SelectionBrush" Value="{StaticResource PetAccentBright}"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TextBox">
          <Border x:Name="Box" CornerRadius="8" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                  BorderThickness="{TemplateBinding BorderThickness}" SnapsToDevicePixels="True">
            <ScrollViewer x:Name="PART_ContentHost" Margin="{TemplateBinding Padding}" Focusable="False"
                          HorizontalScrollBarVisibility="Hidden" VerticalScrollBarVisibility="Hidden"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource PetAccentBright}"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource PetAccent}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType="ComboBoxItem">
    <Setter Property="Foreground" Value="{StaticResource PetInk}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBoxItem">
          <Border x:Name="Row" CornerRadius="6" Padding="8,5,8,5" Margin="0,1,0,1" Background="Transparent" SnapsToDevicePixels="True">
            <ContentPresenter VerticalAlignment="Center"/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="Row" Property="Background" Value="{StaticResource PetAccentSofter}"/>
            </Trigger>
            <Trigger Property="IsHighlighted" Value="True">
              <Setter TargetName="Row" Property="Background" Value="{StaticResource PetAccentSoft}"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Read-only drop-down: a rounded box with a chevron and a rounded, shadowed list. -->
  <Style TargetType="ComboBox">
    <Setter Property="Foreground" Value="{StaticResource PetInk}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{StaticResource PetFocus}"/>
    <Setter Property="MinHeight" Value="30"/>
    <Setter Property="ScrollViewer.CanContentScroll" Value="True"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="ComboBox">
          <Grid SnapsToDevicePixels="True">
            <ToggleButton x:Name="Toggle" Focusable="False" ClickMode="Press"
                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border x:Name="Box" CornerRadius="8" Background="White" BorderBrush="{StaticResource PetControlEdge}" BorderThickness="1">
                    <TextBlock Text="&#xE70D;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" FontSize="10" Foreground="{StaticResource PetMuted}"
                               HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,10,0"/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                      <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource PetAccentBright}"/>
                    </Trigger>
                    <Trigger Property="IsChecked" Value="True">
                      <Setter TargetName="Box" Property="BorderBrush" Value="{StaticResource PetAccent}"/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter IsHitTestVisible="False" Margin="10,4,28,4" VerticalAlignment="Center" HorizontalAlignment="Left"
                              Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
                              ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}"
                              ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"/>
            <Popup x:Name="PART_Popup" Placement="Bottom" HorizontalOffset="-4" VerticalOffset="0" AllowsTransparency="True" Focusable="False"
                   PopupAnimation="Fade" IsOpen="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}">
              <Grid Margin="4,2,10,12">
                <Grid MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="{TemplateBinding MaxDropDownHeight}">
                  <Border CornerRadius="10" Background="White" Effect="{StaticResource PetPopupShadow}"/>
                  <Border CornerRadius="10" Background="White" BorderBrush="{StaticResource PetLine}" BorderThickness="1" Padding="4">
                    <ScrollViewer SnapsToDevicePixels="True">
                      <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
                    </ScrollViewer>
                  </Border>
                </Grid>
              </Grid>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.5"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Slider: thin rounded track, orange filled part, white knob with an orange ring. -->
  <Style x:Key="PetSliderFill" TargetType="RepeatButton">
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="IsTabStop" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RepeatButton">
          <Grid Background="Transparent">
            <Border Height="4" CornerRadius="2" Background="{TemplateBinding Background}" VerticalAlignment="Center"/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key="PetSliderThumb" TargetType="Thumb">
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Thumb">
          <Ellipse x:Name="Knob" Width="16" Height="16" Fill="White" Stroke="{StaticResource PetAccent}" StrokeThickness="2"/>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Knob" Property="Fill" Value="{StaticResource PetAccentSoft}"/>
            </Trigger>
            <Trigger Property="IsDragging" Value="True">
              <Setter TargetName="Knob" Property="Fill" Value="{StaticResource PetAccentSofter}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType="Slider">
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{StaticResource PetFocus}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Slider">
          <Grid MinHeight="22" Background="Transparent">
            <Border Height="4" CornerRadius="2" Background="{StaticResource PetTrackOff}" VerticalAlignment="Center"/>
            <Track x:Name="PART_Track">
              <Track.DecreaseRepeatButton>
                <RepeatButton Command="Slider.DecreaseLarge" Style="{StaticResource PetSliderFill}" Background="{StaticResource PetAccent}"/>
              </Track.DecreaseRepeatButton>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command="Slider.IncreaseLarge" Style="{StaticResource PetSliderFill}" Background="Transparent"/>
              </Track.IncreaseRepeatButton>
              <Track.Thumb>
                <Thumb Style="{StaticResource PetSliderThumb}"/>
              </Track.Thumb>
            </Track>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Opacity" Value="0.45"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Thin scroll bars without arrow buttons. -->
  <Style x:Key="PetScrollPage" TargetType="RepeatButton">
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="IsTabStop" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="RepeatButton">
          <Border Background="Transparent"/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style x:Key="PetScrollThumb" TargetType="Thumb">
    <Setter Property="Focusable" Value="False"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="Thumb">
          <Border x:Name="Bar" CornerRadius="3" Margin="2" Background="{StaticResource PetControlLine}"/>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Bar" Property="Background" Value="{StaticResource PetAccentBright}"/>
            </Trigger>
            <Trigger Property="IsDragging" Value="True">
              <Setter TargetName="Bar" Property="Background" Value="{StaticResource PetAccent}"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <ControlTemplate x:Key="PetVerticalScroll" TargetType="ScrollBar">
    <Grid Background="Transparent">
      <Track x:Name="PART_Track" IsDirectionReversed="True">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageUpCommand" Style="{StaticResource PetScrollPage}"/>
        </Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageDownCommand" Style="{StaticResource PetScrollPage}"/>
        </Track.IncreaseRepeatButton>
        <Track.Thumb>
          <Thumb Style="{StaticResource PetScrollThumb}"/>
        </Track.Thumb>
      </Track>
    </Grid>
  </ControlTemplate>
  <ControlTemplate x:Key="PetHorizontalScroll" TargetType="ScrollBar">
    <Grid Background="Transparent">
      <Track x:Name="PART_Track">
        <Track.DecreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageLeftCommand" Style="{StaticResource PetScrollPage}"/>
        </Track.DecreaseRepeatButton>
        <Track.IncreaseRepeatButton>
          <RepeatButton Command="ScrollBar.PageRightCommand" Style="{StaticResource PetScrollPage}"/>
        </Track.IncreaseRepeatButton>
        <Track.Thumb>
          <Thumb Style="{StaticResource PetScrollThumb}"/>
        </Track.Thumb>
      </Track>
    </Grid>
  </ControlTemplate>
  <Style TargetType="ScrollBar">
    <Setter Property="MinWidth" Value="0"/>
    <Setter Property="Width" Value="10"/>
    <Setter Property="Template" Value="{StaticResource PetVerticalScroll}"/>
    <Style.Triggers>
      <Trigger Property="Orientation" Value="Horizontal">
        <Setter Property="Width" Value="Auto"/>
        <Setter Property="MinHeight" Value="0"/>
        <Setter Property="Height" Value="10"/>
        <Setter Property="Template" Value="{StaticResource PetHorizontalScroll}"/>
      </Trigger>
    </Style.Triggers>
  </Style>

  <!-- Tabs as a sidebar: icon (TabItem.Tag) and name, the chosen page highlighted. -->
  <Style TargetType="TabControl">
    <Setter Property="TabStripPlacement" Value="Left"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TabControl">
          <Grid SnapsToDevicePixels="True" KeyboardNavigation.TabNavigation="Local">
            <Grid.ColumnDefinitions>
              <ColumnDefinition Width="150"/>
              <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>
            <Border Background="{StaticResource PetStage}" BorderBrush="{StaticResource PetLine}" BorderThickness="0,0,1,0">
              <TabPanel IsItemsHost="True" Margin="8,12,8,8" KeyboardNavigation.TabIndex="1"/>
            </Border>
            <ContentPresenter x:Name="PART_SelectedContentHost" Grid.Column="1" ContentSource="SelectedContent"/>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType="TabItem">
    <Setter Property="Foreground" Value="{StaticResource PetInk}"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{StaticResource PetFocus}"/>
    <Setter Property="Template">
      <Setter.Value>
        <ControlTemplate TargetType="TabItem">
          <Border x:Name="Row" CornerRadius="8" Padding="10,8,10,8" Margin="0,2,0,2" Background="Transparent">
            <StackPanel Orientation="Horizontal">
              <Border Background="{StaticResource PetAccentSoft}" CornerRadius="6" Padding="4" IsHitTestVisible="False">
              <TextBlock x:Name="Glyph" Width="18" FontSize="14" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" VerticalAlignment="Center"
                         Foreground="{StaticResource PetAccent}" Text="{Binding Tag, RelativeSource={RelativeSource TemplatedParent}}"/>
              </Border>
              <ContentPresenter x:Name="Label" ContentSource="Header" Margin="8,0,0,0" VerticalAlignment="Center" RecognizesAccessKey="True"/>
            </StackPanel>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Row" Property="Background" Value="{StaticResource PetAccentSoft}"/>
            </Trigger>
            <Trigger Property="IsSelected" Value="True">
              <Setter TargetName="Row" Property="Background" Value="{StaticResource PetAccentSofter}"/>
              <Setter TargetName="Glyph" Property="Foreground" Value="{StaticResource PetAccent}"/>
              <Setter TargetName="Label" Property="TextElement.FontWeight" Value="SemiBold"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>
""";

    private static ResourceDictionary? resources;
    // The styles alone, for a part of a window (the Expedition map's thin scroll bar).
    public static ResourceDictionary Resources => resources ??= (ResourceDictionary)XamlReader.Parse(Xaml);

    // Merges the styles into a window and, on Windows 11, tints its title bar cream with brown text.
    public static void Apply(Window window)
    {
        window.Resources.MergedDictionaries.Add(Resources);
        window.SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(window).Handle;
            SetColor(handle, CaptionColor, PetUi.SurfaceHex);
            SetColor(handle, TextColor, PetUi.InkHex);
        };
    }

    private const int CaptionColor = 35, TextColor = 36;

    // DWM wants 0x00BBGGRR; older Windows rejects the attribute and keeps its own title bar.
    private static void SetColor(IntPtr handle, int attribute, string hex)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        int value = color.R | color.G << 8 | color.B << 16;
        try { _ = DwmSetWindowAttribute(handle, attribute, ref value, sizeof(int)); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
