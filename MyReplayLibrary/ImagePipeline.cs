using OpenCvSharp;

namespace MyReplayLibrary;

public class ImagePipeline(string latestSs, int cornerX, int cornerY, bool redTeam) {
    private bool _init;
    //private const int OutHeight = 18;
    //private const int OutWidth = 110;
    //private const int InHeight = 95;
    //private const int InWidth = 121;

    private static int _cbase;
    private readonly int _c = ++_cbase;

    public Mat Image { get; set; } = null!;

    public void FromFile(int inWidth, int inHeight) {
        var preImg1 = Cv2.ImRead(latestSs)[new Rect(cornerX, cornerY, inWidth, inHeight)];
        Image = preImg1;
        _init = true;
    }

    public void Greyscale() {
        if (!_init) {
            throw new InvalidOperationException("Not initialized");
        }

        var preImg = Image.EmptyClone();
        Cv2.CvtColor(Image, preImg, ColorConversionCodes.BGR2GRAY);
        Image.Dispose();
        Image = preImg;
    }

    public void Threshold(int thresh) {
        var cv2Img1 = Image;
        var cv2Img = cv2Img1.EmptyClone();
        //var thresh = redTeam ? 100 : 110;
        Cv2.Threshold(cv2Img1, cv2Img, thresh, 255, ThresholdTypes.BinaryInv);
        Image.Dispose();
        Image = cv2Img;
    }

    public void Rotate(int factor, double angle) {
        var sz = Image.Size();
        var newSize = new Size(sz.Width * factor * 2, sz.Height * factor * 2);
        var preImg2 = new Mat(newSize, Image.Type());
        var xOrig = redTeam ? sz.Width : 0;
        var rotMat = Cv2.GetRotationMatrix2D(new Point2f(xOrig, 0), -angle, factor);
        Cv2.WarpAffine(Image, preImg2, rotMat, preImg2.Size(), InterpolationFlags.Cubic);
        Image.Dispose();
        Image = preImg2;
    }

    public void Trim(int left, int top, int right, int bottom, int outWidth, int outHeight) {
        var wd = Image.Width;
        var ht = Image.Height;
        using var newImg = Image[top, ht - bottom, left, wd - right];
        var rowEnd = Math.Min(outHeight, ht - bottom);
        var colEnd = Math.Min(outWidth, wd - right);
        var newImg2 = newImg[0, rowEnd, 0, colEnd];
        Image.Dispose();
        Image = newImg2;
    }

    public void Scale(double factor) {
        var wd = Image.Width;
        var ht = Image.Height;
        var newSize = new Size(wd * factor, ht * factor);
        var newImg = Image.Resize(newSize, 0, 0, InterpolationFlags.Cubic);
        Image.Dispose();
        Image = newImg;
    }

    public void Sharpen() {
        var cv2Tgt = Image.EmptyClone();
        var kernel = new Mat(new Size(3, 3), MatType.CV_32F); // new float[] { -1, -1, -1, -1, 9, -1, -1, -1, -1 });
        kernel.SetArray(new float[] { -1, -1, -1, -1, 9, -1, -1, -1, -1 });
        Cv2.Filter2D(Image, cv2Tgt, Image.Depth(), kernel);
        Image.Dispose();
        Image = cv2Tgt;
    }

    public byte[] GetSaveImage(string suffix) {
        var cv2Bytes = Image.ImEncode(".jpg");

        //File.WriteAllBytes($"test{_c}{suffix}_{cornerX}_{cornerY}.jpg", cv2Bytes);

        return cv2Bytes;
    }
}
