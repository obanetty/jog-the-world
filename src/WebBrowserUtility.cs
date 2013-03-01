using System;
using System.Windows.Controls;
using System.Windows;

namespace WpfGoogleMapClient
{
	/// <summary>
	/// WebBrowser クラス用の Source プロパティへの Binding 機能を提供する為のユーティリティ クラスです。
	/// </summary>
	public static class WebBrowserUtility
	{
		/// <summary>
		/// BindableSourceProperty を取得します。
		/// </summary>
		/// <param name="obj">依存プロパティ。</param>
		/// <returns>取得した値。</returns>
		public static string GetBindableSource( DependencyObject obj )
		{
			return ( string )obj.GetValue( BindableSourceProperty );
		}

		/// <summary>
		/// BindableSourceProperty を設定します。
		/// </summary>
		/// <param name="obj">依存プロパティ。</param>
		/// <param name="value">設定する値。</param>
		public static void SetBindableSource( DependencyObject obj, string value )
		{
			obj.SetValue( BindableSourceProperty, value );
		}

		/// <summary>
		/// BindableSourceProperty が変更された時に発生します。
		/// </summary>
		/// <param name="o">依存プロパティ。</param>
		/// <param name="e">イベント データ。</param>
		public static void BindableSourcePropertyChanged( DependencyObject o, DependencyPropertyChangedEventArgs e )
		{
			WebBrowser browser = o as WebBrowser;
			if( browser != null )
			{
				string uri = e.NewValue as string;
				browser.Source = uri != null ? new Uri( uri ) : null;
			}
		}

		/// <summary>
		/// WebBrowser クラスの Source プロパティへの Binding 機能を提供する為の依存プロパティです。
		/// </summary>
		public static readonly DependencyProperty BindableSourceProperty = DependencyProperty.RegisterAttached( "BindableSource", typeof( string ), typeof( WebBrowserUtility ), new UIPropertyMetadata( null, BindableSourcePropertyChanged ) );
	}
}
