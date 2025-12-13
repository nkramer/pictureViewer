using System;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Folio.Core {
    public class GrayscaleEffect : ShaderEffect {
        static GrayscaleEffect() {
            _pixelShader = new PixelShader();
            //_pixelShader.UriSource = new Uri("GrayscaleShader.fx.ps", UriKind.RelativeOrAbsolute); // sl
            Assembly a = typeof(GrayscaleEffect).Assembly;
            string assemblyShortName = a.ToString().Split(',')[0];
            _pixelShader.UriSource = new Uri(@"pack://application:,,,/" + assemblyShortName + ";component/GrayscaleShader.fx.ps"); // wpf
        }

        public GrayscaleEffect() {
            this.PixelShader = _pixelShader;
            this.DdxUvDdyUvRegisterIndex = 0;
        }

        public Brush Input {
            get { return (Brush)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        // ShaderEffect.RegisterPixelShaderSamplerProperty will cause
        // Silverlight to use the visual representationk of the element
        // this shader is attached to as the shader input
        public static readonly DependencyProperty InputProperty =
            ShaderEffect.RegisterPixelShaderSamplerProperty(
                    "Input",
                    typeof(GrayscaleEffect),
                    0);

        static PixelShader _pixelShader;
    }
}
