using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace LighthouseGame
{
    public sealed partial class KeeperMenu
    {
        private int _currentPhoto;
        private Button _photoReturn;
        private Texture2D _viewerTexture;

        private void RenderGallery()
        {
            ClearGallery();
            Text("best-score",_archive.Data.Best.ToString("N0",Russian)+" очков");
            Text("archive-status",_archive.Error??"");
            Show("empty-archive",_archive.Data.Photos.Count==0);
            var grid=_root.Q("photo-grid");
            var featured=new VisualElement{name="featured-row"};featured.AddToClassList("featured-row");
            var side=new VisualElement();side.AddToClassList("featured-side");featured.Add(side);
            if(_archive.Data.Photos.Count>0)grid.Add(featured);
            if(_archive.Data.Photos.Count<=3)featured.style.marginBottom=0;
            if(!_root.Q("menu").ClassListContains("compact"))featured.style.height=Mathf.Max(350,_root.resolvedStyle.height-235);
            for(int i=0;i<_archive.Data.Photos.Count;i++)
            {
                int index=i;var photo=_archive.Data.Photos[i];
                var card=new Button{name="archive-photo-"+i};card.AddToClassList("photo-card");
                if(i==0)card.AddToClassList("hero");
                if(i==Mathf.Min(2,_archive.Data.Photos.Count-1))card.AddToClassList("last-featured");
                card.clicked+=()=>{_photoReturn=card;OpenPhoto(index);};
                var image=_archive.LoadImage(photo);
                if(image){_archiveImages.Add(image);var preview=new Image{image=image,scaleMode=ScaleMode.ScaleToFit};preview.AddToClassList("photo-image");card.Add(preview);}
                else{var missing=new Label("Изображение недоступно");missing.AddToClassList("photo-missing");card.Add(missing);}
                var caption=new VisualElement();caption.AddToClassList("photo-caption");
                var title=new Label(photo.Label);title.AddToClassList("photo-title");title.style.unityFontDefinition=FontDefinition.FromSDFFont(_displayFont);
                var price=new Label(photo.Price.ToString("N0",Russian)+" очк.");price.AddToClassList("photo-price");price.style.unityFontDefinition=FontDefinition.FromSDFFont(_displayFont);
                caption.Add(title);caption.Add(price);card.Add(caption);
                var rank=new Label((i+1).ToString("D2"));rank.AddToClassList("photo-rank");card.Add(rank);
                var left=new Image{pickingMode=PickingMode.Ignore};left.AddToClassList("photo-mount");SetImage(left,"KeeperMenu/photo-mount");card.Add(left);
                var right=new Image{pickingMode=PickingMode.Ignore};right.AddToClassList("photo-mount");right.AddToClassList("right-mount");SetImage(right,"KeeperMenu/photo-mount");card.Add(right);
                if(i==0)featured.Insert(0,card);else if(i<3)side.Add(card);else grid.Add(card);
            }
        }

        private void OpenPhoto(int index)
        {
            if(_archive.Data.Photos.Count==0)return;
            _currentPhoto=(index+_archive.Data.Photos.Count)%_archive.Data.Photos.Count;
            var photo=_archive.Data.Photos[_currentPhoto];
            if(_viewerTexture)Destroy(_viewerTexture);
            _viewerTexture=_archive.LoadImage(photo);
            _root.Q<Image>("viewer-image").image=_viewerTexture;_root.Q<Image>("viewer-image").scaleMode=ScaleMode.ScaleToFit;
            Show("viewer-missing",!_viewerTexture);
            Text("viewer-position",(_currentPhoto+1)+" / "+_archive.Data.Photos.Count);
            Text("viewer-title",photo.Label);Text("viewer-price",photo.Price.ToString("N0",Russian)+" очк.");
            Text("viewer-subjects",photo.Subjects==null?"":string.Join(" · ",photo.Subjects.Select(subject=>subject.Label+" +"+subject.Price)));
            Show("photo-viewer",true);_root.Q<Button>("viewer-close").Focus();
        }
        private void ClosePhoto()
        {
            if(_root==null)return;
            bool wasOpen=IsShown("photo-viewer");Show("photo-viewer",false);
            _root.Q<Image>("viewer-image").image=null;
            if(_viewerTexture)Destroy(_viewerTexture);_viewerTexture=null;
            if(wasOpen)_photoReturn?.Focus();
        }
        private void ClearGallery()
        {
            _root?.Q("photo-grid")?.Clear();
            foreach(var texture in _archiveImages)if(texture)Destroy(texture);
            _archiveImages.Clear();_photoReturn=null;
        }
    }
}
