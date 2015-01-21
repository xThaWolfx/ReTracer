﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ReTracer.EventArgs;
using ReTracer.Rendering;
using ReTracer.Settings;

namespace ReTracer.Abstract
{
    public abstract class Renderer
    {
        public event EventHandler<RenderStartEventArgs> OnStart; 
        public event EventHandler<RenderProgressEventArgs> OnProgress;
        public event EventHandler<RenderFinishedEventArgs> OnFinish;
        protected PixelColor [ ] Pixels;
        protected uint [ ] PixelSamples;
        protected Scene CurrentScene { private set; get; }
        private readonly Stopwatch Watch = new Stopwatch( );

        public void Render( Scene RenderScene, RenderSettings Settings )
        {
            this.CurrentScene = RenderScene;
            int W = RenderScene.Camera.Resolution.IntX;
            int H = RenderScene.Camera.Resolution.IntY;
            this.Pixels = new PixelColor[ W * H ];

            Parallel.For( 0, this.Pixels.Length, Var =>
            {
                this.Pixels[ Var ] = new PixelColor( );
            } );
            this.PixelSamples = new uint[ this.Pixels.Length ];

            int AreaWidth = ( int ) Math.Ceiling( RenderScene.Camera.Resolution.X / Settings.AreaDivider );
            int AreaHeight = ( int ) Math.Ceiling( RenderScene.Camera.Resolution.Y / Settings.AreaDivider );

            Watch.Restart( );
            for ( int X = 0; X < W; X += AreaWidth )
            {
                int Width = Math.Min( X + AreaWidth, W ) - X;

                for ( int Y = 0; Y < H; Y += AreaHeight )
                {
                    int Height = Math.Min( Y + AreaHeight, H ) - Y;
                    this.RenderRegion( Settings, X, Y, Width, Height );
                }
            }
            Watch.Stop( );

            Finished( );
        }

        protected int ConvertPixelCoordinatesToArrayIndex( int RealX, int RealY )
        {
            return RealY * CurrentScene.Camera.Resolution.IntX + RealX;
        }

        protected abstract void RenderRegion( RenderSettings Settings, int StartX, int StartY, int Width, int Height );

        protected void ReportProgress( int StartX, int StartY, int Width, int Height )
        {
            Console.WriteLine("Progress");
        }

        private void Finished( )
        {
            if ( OnFinish == null ) return;

            Bitmap I = new Bitmap( CurrentScene.Camera.Resolution.IntX, CurrentScene.Camera.Resolution.IntY,
                PixelFormat.Format32bppArgb );

            BitmapData Data = I.LockBits( new Rectangle( 0, 0, I.Width, I.Height ), ImageLockMode.WriteOnly,
                I.PixelFormat );

            int BPP = Image.GetPixelFormatSize( I.PixelFormat ) / 8;
            byte [ ] Bytes = new byte[ Pixels.Length * BPP ];

            Parallel.For( 0, Pixels.Length, Var =>
            {
                Color C = ( Pixels[ Var ] / PixelSamples[ Var ] ).ToColor( );

                Var *= BPP;

                Bytes[ Var ] = C.B;
                Bytes[ Var + 1 ] = C.G;
                Bytes[ Var + 2 ] = C.R;
                Bytes[ Var + 3 ] = 255;
            } );

            Marshal.Copy( Bytes, 0, Data.Scan0, Bytes.Length );
            I.UnlockBits( Data );

            OnFinish.Invoke( this, new RenderFinishedEventArgs
            {
                Image = I,
                RenderTime = Watch.Elapsed
            } );
        }
    }
}
