﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace Pictureviewer.Core {
    public class GrayscaleEffect : ShaderEffect
    {
        static GrayscaleEffect()
        {
            _pixelShader = new PixelShader();
            //_pixelShader.UriSource = new Uri("GrayscaleShader.fx.ps", UriKind.RelativeOrAbsolute); // sl
            _pixelShader.UriSource = new Uri(@"pack://application:,,,/pictureviewer;component/GrayscaleShader.fx.ps"); // wpf
        }

        public GrayscaleEffect()
        {
            this.PixelShader = _pixelShader;
            this.DdxUvDdyUvRegisterIndex = 0;
        }

        public Brush Input
        {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        /// <summary>
        /// ShaderEffect.RegisterPixelShaderSamplerProperty will cause
        /// Silverlight to use the visual representationk of the element
        /// this shader is attached to as the shader input
        /// </summary>
        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty(
                    "Input",
                    typeof(GrayscaleEffect),
                    0);

        static PixelShader _pixelShader;
    }
}
