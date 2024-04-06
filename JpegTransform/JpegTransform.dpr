program JpegTransform;

{$APPTYPE CONSOLE}

uses
  FastMM4, SysUtils, JPEG, dExif, dIPTC;

procedure Rotate(const SrcFileName, DstFileName: string; Angle: Integer);
var
  JPEG : TJPEGImage;
  EXIF : TImgData;
begin
  EXIF:=TImgData.Create;
  JPEG:=TJPEGImage.Create;
  try
    JPEG.LoadFromFile(SrcFileName);
    if Angle=90 then JPEG.Transform(jt_ROT_90)
    else if Angle=180 then JPEG.Transform(jt_ROT_180)
    else if Angle=270 then JPEG.Transform(jt_ROT_270)
    else if Angle<>0 then raise Exception.Create('Unsupported angle');
    try
      if not EXIF.ProcessFile(SrcFileName) and EXIF.HasExif then FreeAndNil(EXIF);
    except
      FreeAndNil(EXIF);
    end;
    if (EXIF<>nil) and (EXIF.ExifObj<>nil) then
    begin
      EXIF.ExifObj.RemoveThumbnail;
      if EXIF.ExifObj.GetRawInt('Orientation')>1 then EXIF.ExifObj.WriteThruInt('Orientation',1);
      EXIF.WriteEXIFJpeg(JPEG,DstFileName);
    end
    else JPEG.SaveToFile(DstFileName);
  finally
    JPEG.Free;
    EXIF.Free;
  end;
end;

procedure Crop(const SrcFileName, DstFileName: string; XOffset, YOffset, NewWidth, NewHeight: Integer);
var
  JPEG : TJPEGImage;
  EXIF : TImgData;
  Orientation : string;
begin
  EXIF:=TImgData.Create;
  JPEG:=TJPEGImage.Create;
  try
    JPEG.LoadFromFile(SrcFileName);
    try
      if not EXIF.ProcessFile(SrcFileName) and EXIF.HasExif then FreeAndNil(EXIF);
    except
      FreeAndNil(EXIF);
    end;

    if (EXIF<>nil) and (EXIF.ExifObj<>nil) then
    begin
      Orientation:=EXIF.ExifObj.LookupTagVal('Orientation');
      if Orientation='Clockwise 90°' then JPEG.Transform(jt_ROT_270)
      else if Orientation='CounterClockwise 90°' then JPEG.Transform(jt_ROT_90)
      else if Orientation='Rotated 180°' then JPEG.Transform(jt_ROT_180);
    end;

    JPEG.Crop(XOffset,YOffset,NewWidth,NewHeight);

    if (EXIF<>nil) and (EXIF.ExifObj<>nil) then
    begin
      EXIF.ExifObj.RemoveThumbnail;
      if EXIF.ExifObj.GetRawInt('Orientation')>1 then EXIF.ExifObj.WriteThruInt('Orientation',1);
      EXIF.WriteEXIFJpeg(JPEG,DstFileName);
    end
    else JPEG.SaveToFile(DstFileName);
  finally
    JPEG.Free;
    EXIF.Free;
  end;
end;

begin
  try
    if ParamCount=3 then Rotate(ParamStr(1),ParamStr(2),StrToInt(ParamStr(3)))
    else if ParamCount=6 then Crop(ParamStr(1),ParamStr(2),StrToInt(ParamStr(3)),StrToInt(ParamStr(4)),StrToInt(ParamStr(5)),StrToInt(ParamStr(6)))
    else raise Exception.Create('Unexpected number of parameters');
  except
    on E: Exception do
    begin
      WriteLn(E.Message);
      WriteLn('---');
      WriteLn('JpegTransform <SrcFileName> <DstFileName> <Angle>');
      WriteLn('or ');
      WriteLn('JpegTransform <SrcFileName> <DstFileName> <XOffset> <YOffset> <NewWidth> <NewHeight>');
      ExitCode:=1;
    end;
  end;
end.

