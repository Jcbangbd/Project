using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UserMiscellaneous/FpsHelper")]
public class FpsHelper : MonoBehaviour 
{
    private float _updateInterval = 1f;//设定更新帧率的时间间隔为1秒  
    private float _accum = .0f;//累积时间  
    private int _frames = 0;//在_updateInterval时间内运行了多少帧  
    private float _timeLeft;
    [SerializeField]
    Text fpsText = null;

	// Use this for initialization
	void Start () 
    {
        if (!fpsText)
            fpsText = GetComponent<Text>();
        if (!fpsText)
            throw new UnityException("FPS Text is't exist!");
        _timeLeft = _updateInterval;  
	}
	
	// Update is called once per frame
	void Update ()
    {
        _timeLeft -= Time.deltaTime;

        //相除即可得到相应的一帧所用的时间  
        _accum += Time.timeScale / Time.deltaTime;
        ++_frames;//帧数  

        if (_timeLeft <= 0)
        {
            float fps = _accum / _frames;

            fpsText.text = System.String.Format("FPS:{0:F2}", fps);//保留两位小数  

            _timeLeft = _updateInterval;
            _accum = .0f;
            _frames = 0;
        }  
	}
}
