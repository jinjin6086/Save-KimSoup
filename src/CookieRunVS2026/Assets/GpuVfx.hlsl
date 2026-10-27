// GPU QTE VFX prototype. All endpoints are in final backbuffer UV space.
// Widths are physical backbuffer pixels; procedural detail scales with sceneRect.
// b0 contract (float4[16], 256 bytes):
// 0 outputWidth, outputHeight, animationSeconds, intensity
// 1 blueStartUV.xy, blueEndUV.zw
// 2 pinkStartUV.xy, pinkEndUV.zw
// 3 clashUV.xy, blueHalfWidthPx, mode (0 off,1 charge,2 pink,3 clash,4 finish)
// 4 phaseAgeSec, clashStrength, pinkChargeStrength, finishStrength
// 5 heroChargeStrength, pinkHalfWidthPx, blueBeamAlpha, pinkBeamAlpha
// 6 explosionUV.xy, explosionAgeSec, explosionStrength
// 7 sceneRectUV (left, top, width, height)
// 8 impactMode (0 normal,1 ink on white,2 white on black,3 silence),
//   distortionStrength, quality (reserved), pressurePulse
// 9 useAuthoredPlasma, blueFrameIndex, pinkFrameIndex, reserved (40 slices).
// 10 useAuthoredContact, contactFrameIndex, contactHalfWidthPx, contactHalfHeightPx.
// 11 useAuthoredDetonation, detonationFrame, halfWidthPx, halfHeightPx.
// 12 detonationFade, normalizedBlastProgress, reserved, reserved.
// 13 inkDetailEnabled, whiteBackground, impactRadiusPx, impactAgeSec.
// 14 inkCenterUV.xy, grayscaleVolumeFrame, reserved.

Texture2D sceneTex : register(t0);
Texture2DArray<float4> bluePlasma : register(t1);
Texture2DArray<float4> pinkPlasma : register(t2);
Texture2DArray<float4> contactPlasma : register(t3);
Texture2DArray<float4> detonationPlasma : register(t4);
SamplerState sceneSampler : register(s0);
cbuffer VfxConstants : register(b0) { float4 v[16]; }

struct VertexOut { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
VertexOut VSMain(uint id : SV_VertexID)
{
    VertexOut o;
    o.uv = float2((id << 1) & 2, id & 2);
    o.position = float4(o.uv * float2(2,-2) + float2(-1,1), 0, 1);
    return o;
}

static const float PI = 3.14159265359;
static const float3 BLUE = float3(0.012, 0.21, 1.0);
static const float3 CYAN = float3(0.08, 0.72, 1.0);
static const float3 PINK = float3(1.0, 0.025, 0.30);
static const float3 ROSE = float3(1.0, 0.20, 0.61);

float hash11(float p) { return frac(sin(p * 127.1) * 43758.5453); }
float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453); }
float noise(float2 p)
{
    float2 i = floor(p), f = frac(p);
    f = f*f*(3.0-2.0*f);
    return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),
                lerp(hash21(i+float2(0,1)),hash21(i+float2(1,1)),f.x),f.y);
}
float flowNoise(float2 p)
{
    float n = noise(p)*0.57;
    p = float2(p.x*1.87-p.y*0.33,p.x*0.24+p.y*1.93);
    n += noise(p)*0.28;
    return n + noise(p*2.07)*0.15;
}
float lineMask(float distance, float halfWidth)
{
    float aa = max(fwidth(distance)*0.7,0.70);
    return 1.0-smoothstep(halfWidth-aa,halfWidth+aa,distance);
}
float segmentDistance(float2 p, float2 a, float2 b)
{
    float2 d=b-a;
    return length(p-a-d*saturate(dot(p-a,d)/max(dot(d,d),0.01)));
}
float gaussian(float x) { return exp2(-x*x*1.442695); }

// Separated radiance/coverage lets white cores stay compact while the body stays colored.
struct Energy { float3 light; float silhouette; float hot; float distortion; float occlusion; };
Energy zeroEnergy()
{
    Energy e; e.light=0; e.silhouette=0; e.hot=0; e.distortion=0; e.occlusion=0; return e;
}
void addEnergy(inout Energy a, Energy b, float weight)
{
    a.light += b.light*weight;
    a.silhouette=max(a.silhouette,b.silhouette*weight);
    a.hot=max(a.hot,b.hot*weight);
    a.distortion += b.distortion*weight;
    a.occlusion=max(a.occlusion,b.occlusion*weight);
}

// Premultiplied authored volume frames retain their cool dark folds and fine
// luminous material. Frame interpolation is continuous even during slow motion.
float4 plasmaSample(float2 uv,float pink,float frameOffset)
{
    if(uv.x<0 || uv.x>1 || uv.y<0 || uv.y>1) return 0;
    float frame=(pink>.5 ? v[9].z : v[9].y)+frameOffset;
    frame=frac(frame/40.0)*40.0;
    float a=floor(frame),b=a>=39 ? 0 : a+1;
    float4 first,second;
    if(pink>.5)
    {
        first=pinkPlasma.SampleLevel(sceneSampler,float3(uv,a),0);
        second=pinkPlasma.SampleLevel(sceneSampler,float3(uv,b),0);
    }
    else
    {
        first=bluePlasma.SampleLevel(sceneSampler,float3(uv,a),0);
        second=bluePlasma.SampleLevel(sceneSampler,float3(uv,b),0);
    }
    return lerp(first,second,frac(frame));
}

float4 contactSample(float2 uv,float frameOffset)
{
    if(uv.x<0 || uv.x>1 || uv.y<0 || uv.y>1) return 0;
    float frame=frac((v[10].y+frameOffset)/40.0)*40.0;
    float a=floor(frame),b=a>=39 ? 0 : a+1;
    return lerp(contactPlasma.SampleLevel(sceneSampler,float3(uv,a),0),
                contactPlasma.SampleLevel(sceneSampler,float3(uv,b),0),frac(frame));
}

Energy authoredBeam(float2 q,float len,float w,float time,float pink)
{
    Energy e=zeroEnergy();
    if(q.x<0 || q.x>len || abs(q.y)>w*2.0) return e;
    float u=q.x/len;
    float coarse=flowNoise(float2(q.x*.036-time*2.4,q.y/w*2.8+time*.5));
    float micro=noise(float2(q.x*.23-time*13,q.y*.15+time*6));
    float throat=sin(saturate(u)*PI);
    float warpU=(coarse-.5)*.012*throat;
    float warpV=(coarse-.5)*.027*throat+.007*sin(q.x*.083-time*8.2);
    float tipExtent=min(w*2.1,len*.24);
    float tipProgress=saturate((q.x-(len-tipExtent))/max(tipExtent,.01));
    float noseWidth=sqrt(max(.0004,1-tipProgress*tipProgress));
    float2 materialUV=float2(clamp(u+warpU,.001,.999),.5+q.y/(w*3.0*noseWidth)+warpV);
    float4 raw=plasmaSample(materialUV,pink,0);
    // A second advected fine sample adds microglints without moving the contour.
    float4 detail=plasmaSample(materialUV+float2(.006*sin(time*2.1+u*23),.009*(micro-.5)),pink,3.35);
    float alpha=raw.a;
    float3 rgb=raw.rgb/max(alpha,.002);
    float3 detailRGB=detail.rgb/max(detail.a,.002);
    float flicker=1+.055*sin(time*61+q.x*.43)*micro;
    float white=smoothstep(.58,.94,min(rgb.r,min(rgb.g,rgb.b)));
    float glints=pow(saturate(dot(detailRGB,float3(.22,.56,.22))-.57)*2.3,3);
    float3 color=lerp(BLUE,PINK,pink),inner=lerp(CYAN,ROSE,pink);
    e.light=pow(max(rgb,0),1.48)*alpha*(1.85+.28*coarse)*flicker;
    e.light += float3(.94,.985,1)*white*alpha*.64;
    e.light += inner*glints*detail.a*.48;
    // The needle is much narrower than the material, so dark folded ribbons stay visible.
    float axisMask=lineMask(abs(q.y),max(.55,w*.047))*
                   smoothstep(0,.018,u)*(1-smoothstep(.985,1,u));
    float needleFlow=.30+.70*smoothstep(.32,.88,noise(float2(q.x*.15-time*17,q.y*.31)));
    e.light += float3(1,.98,1)*axisMask*needleFlow*1.55;
    e.light += color*gaussian(q.y/(w*1.02))*.047*smoothstep(0,.03,u)*(1-smoothstep(.96,1,u));
    e.silhouette=alpha;
    e.hot=max(axisMask*.6,white*alpha);
    e.occlusion=alpha*(.88-.40*white);
    e.distortion=alpha*(.20+.35*coarse);
    // Layered particles sit just in front of and behind the authored volume.
    [unroll] for(int lane=0;lane<7;lane++)
    {
        float z=hash11(lane*13.37+pink*21);
        float spacing=26+z*48;
        float moving=q.x-time*(65+z*150);
        float cell=floor(moving/spacing);
        float r=hash11(cell*17.93+lane*8.17+pink*31);
        float x=frac(moving/spacing)*spacing-spacing*r;
        float side=lane%2==0 ? -1.0 : 1.0;
        float y=side*w*(.44+z*.95)+sin(q.x*.025+lane*3.1+time*2)*w*.07;
        float shape=gaussian(x/(.85+z*2.8))*gaussian((q.y-y)/(.35+z*.90));
        float fade=sin(u*PI)*(.25+.75*z);
        e.light += lerp(inner,float3(.97,.99,1),step(.83,z))*shape*fade*(.4+z*1.9);
    }
    // A rounded parabolic nose softens an approaching beam before contact.
    // It also prevents the animated material's last column becoming a hard cut.
    float capLength=tipExtent;
    float capX=max(0,(q.x-(len-capLength))/max(capLength,.01));
    float capShape=capX*capX+pow(q.y/max(w*1.50,.01),2);
    float capAA=max(fwidth(capShape)*.85,.006);
    float cap=capX>0 ? 1-smoothstep(1-capAA,1+capAA,capShape) : 1;
    cap*=1-smoothstep(len-capLength*.55,len,q.x);
    e.light*=cap;e.silhouette*=cap;e.hot*=cap;e.distortion*=cap;e.occlusion*=cap;
    return e;
}

Energy beam(float2 p,float2 start,float2 end,float width,float time,float pink,float pulse)
{
    Energy e=zeroEnergy();
    float2 delta=end-start;
    float beamLength=length(delta);
    if(beamLength<2 || width<0.5) return e;
    float2 axis=delta/beamLength, normal=float2(-axis.y,axis.x);
    float2 q=float2(dot(p-start,axis),dot(p-start,normal));
    float w=max(width,1.25);
    if(q.x < -w*2.6 || q.x>beamLength+w*0.45 || abs(q.y)>w*4.4) return e;
    if(v[9].x>.5) return authoredBeam(q,beamLength,w,time,pink);
    float u=saturate(q.x/beamLength);
    float longitudinal=smoothstep(-w*.25,w*.28,q.x)*(1-smoothstep(beamLength-w*.1,beamLength+w*.20,q.x));
    float t=time+(pink>0.5 ? 1.731 : 0);
    float n=flowNoise(float2(q.x*.022-t*2.3,q.y/w*1.5+t*.17));
    float n2=noise(float2(q.x*.049-t*5.3,q.y/w*3.4-t*.31));
    float taper=.78+.12*sin(u*PI)+.035*sin(q.x*.018-t*6.0);
    float edgeWidth=w*(taper+(n-.5)*.15);
    float centerWobble=w*(.025*sin(q.x*.023-t*4.0)+.025*(n-.5));
    float dy=q.y-centerWobble;
    float body=lineMask(abs(dy),edgeWidth)*longitudinal;
    float depth=gaussian(dy/(w*.73));
    float cutout=smoothstep(.21,.69,n*.73+n2*.27);
    float3 color=lerp(BLUE,PINK,pink), inner=lerp(CYAN,ROSE,pink);

    // The sheath has slower dark gaps and faster inner folds, with clear colored edges.
    e.light += color*body*(.36+.58*depth)*(.43+.72*cutout);
    e.light += inner*body*(.19+.42*n2)*depth;
    float rim=lineMask(abs(abs(dy)-edgeWidth*.91),max(w*.045,.8))*longitudinal;
    e.light += inner*rim*(.25+.38*n);
    float halo=gaussian(dy/(w*1.48))*longitudinal;
    e.light += color*halo*.16;
    e.light += inner*gaussian(dy/(w*.48))*longitudinal*.14;

    // Two broad helical sheets cross in depth. Their opacity differs front to back.
    float helix=q.x*.020-t*7.4;
    float h1=sin(helix)*w*.50;
    float h2=sin(helix+PI)*w*.50;
    float sheet1=lineMask(abs(dy-h1),w*(.075+.04*n2))*body;
    float sheet2=lineMask(abs(dy-h2),w*.095)*body;
    e.light += inner*(sheet1*(.30+.50*saturate(cos(helix)))+sheet2*.23);
    // A straight hot axis anchors the entire silhouette; fast channels live inside it.
    float coreWidth=w*(.112+.018*sin(q.x*.031-t*12.0));
    float core=lineMask(abs(q.y),coreWidth)*longitudinal;
    float hotFlow=.78+.22*noise(float2(q.x*.12-t*17.0,q.y/w*4.0));
    e.light += float3(1.0,.97,1.0)*core*(2.3+.6*hotFlow);
    e.light += inner*gaussian(q.y/(w*.21))*longitudinal*.65;

    // Traveling oblique compression bands, blended into the volume instead of loose wires.
    float train=frac(q.x/max(w*3.6,35)-t*1.25);
    float band=gaussian((train-.50)/.065)*body;
    e.light += inner*band*(.12+.35*abs(dy)/w);

    // Three deterministic depth lanes: sparse sparks are short, bright shapes.
    [unroll] for(int lane=0;lane<3;lane++)
    {
        float cellSize=max(w*3.5,45);
        float travel=q.x-t*(140+lane*47);
        float cell=floor(travel/cellSize);
        float random=hash11(cell*7.13+lane*17.61+pink*51.2);
        float along=frac(travel/cellSize)*cellSize-cellSize*random;
        float side=(lane==1 ? -1.0 : 1.0);
        float sideDistance=side*w*(1.03+random*.8);
        float spark=gaussian(along/(2.0+random*4.0))*gaussian((q.y-sideDistance)/(0.8+random*1.3));
        float nearWeight=lane==2 ? .85 : .38;
        e.light += inner*spark*nearWeight*longitudinal;
    }
    e.silhouette=body;
    e.hot=core;
    e.distortion=halo*(.35+.65*cutout);
    return e;
}

Energy authoredSource(float2 q,float w,float time,float pink,float charge)
{
    Energy e=zeroEnergy();
    float2 s=q/w;
    float r=length(s);
    if(r>2.65) return e;
    float a=atan2(s.y,s.x);
    float n=flowNoise(s*2.3+float2(-time*1.7,time*.75));
    float2 uv=float2(.19+r*.18+.055*sin(a*2.0-time*.8),
                    .5+.37*sin(a+r*1.8-time*.9));
    float4 raw=plasmaSample(uv,pink,7.3);
    float4 crossed=plasmaSample(float2(.31+s.x*.14,.5+s.y*.25+(n-.5)*.12),pink,17.65);
    float3 color=lerp(BLUE,PINK,pink),inner=lerp(CYAN,ROSE,pink);
    float envelope=gaussian(r/(.73+charge*.13))*(.48+.72*n);
    float3 rgb=raw.rgb/max(raw.a,.002);
    float3 crossRGB=crossed.rgb/max(crossed.a,.002);
    e.light=pow(max(rgb,0),1.35)*raw.a*envelope*1.85;
    e.light += pow(max(crossRGB,0),1.5)*crossed.a*gaussian(s.y/.80)*gaussian(s.x/.67)*.90;
    e.light += color*gaussian(r/1.33)*.12;
    // Compact asymmetric star and white throat replace the smooth oval/ring.
    float hot=gaussian(s.x/.17)*gaussian(s.y/.22);
    float starX=gaussian(q.y/max(.65,w*.020))*gaussian(q.x/(w*1.20));
    float starY=gaussian(q.x/max(.60,w*.018))*gaussian(q.y/(w*.70));
    e.light += float3(.96,.99,1)*(hot*2.5+starX*.36+starY*.23);
    e.silhouette=raw.a*envelope;
    e.hot=hot;
    e.occlusion=raw.a*envelope*.40;
    e.distortion=gaussian(r/1.35)*.50;
    [unroll] for(int i=0;i<8;i++)
    {
        float seed=i*11.73+pink*29.4;
        float age=frac(time*(.50+hash11(seed)*.40)+hash11(seed+3));
        float angle=hash11(seed+1)*PI*2;
        float2 dir=float2(cos(angle),sin(angle));
        float2 trailPoint=dir*w*(.58+age*(.7+hash11(seed+2)*1.3));
        trailPoint.x -= age*age*w*.55;
        float sd=segmentDistance(q,trailPoint-dir*w*(.04+age*.10),trailPoint);
        float spark=lineMask(sd,.45+hash11(seed+5)*.55)*(1-age)*.58;
        e.light += inner*spark;
    }
    return e;
}

Energy sourceFlare(float2 p,float2 origin,float2 axis,float width,float time,float pink,float charge)
{
    Energy e=zeroEnergy();
    float2 n=float2(-axis.y,axis.x), rel=p-origin;
    float2 q=float2(dot(rel,axis),dot(rel,n));
    float w=max(width,3.0)*(1.0+.17*charge);
    if(abs(q.x)>w*2.6 || abs(q.y)>w*2.8) return e;
    if(v[9].x>.5) return authoredSource(q,w,time,pink,charge);
    float3 color=lerp(BLUE,PINK,pink), inner=lerp(CYAN,ROSE,pink);
    float nr=flowNoise(q/w*2.1+float2(-time*1.4,time*.6));
    float r=length(q/float2(w*.60,w*.90));
    float sphere=1-smoothstep(.72,1.02+(nr-.5)*.17,r);
    float sourceCore=gaussian(q.x/(w*.25))*gaussian(q.y/(w*.37));
    float edge=lineMask(abs(r-(.89+(nr-.5)*.12))*w,max(.9,w*.045));
    e.light=color*(sphere*.55+gaussian(r/1.4)*.18)+inner*(edge*.7+sphere*.2);
    e.light += float3(1,.98,1)*sourceCore*2.0;
    // Backward bowing pressure sheets keep the emitter attached and directional.
    float y=q.y/w;
    float arcX=-w*(.16+y*y*.48)+(nr-.5)*w*.10;
    float arc=lineMask(abs(q.x-arcX),max(w*.065,.9))*(1-smoothstep(.85,1.8,abs(y)));
    float arc2=lineMask(abs(q.x-arcX+w*.33),max(w*.032,.8))*(1-smoothstep(.9,2.15,abs(y)));
    e.light += inner*arc*.55+color*arc2*.31;
    float rays=pow(abs(cos(atan2(q.y,q.x)*3.0+time*.6)),18.0);
    e.light += inner*rays*gaussian(r/1.65)*.10;
    e.silhouette=sphere;
    e.hot=sourceCore;
    e.distortion=gaussian(r/1.7)*.7;
    return e;
}

Energy authoredClash(float2 q,float w,float time,float pulse)
{
    Energy e=zeroEnergy();
    float y=q.y/w;
    float n=flowNoise(float2(q.x/w*2.9-time*.9,y*3.2-time*2.4));
    float micro=noise(float2(y*17-time*13,q.x/w*10+time*7));
    float seamShift=w*(.040*sin(y*6.4+time*11)+.040*(n-.5)+.024*(micro-.5));
    float bow=w*(.075+y*y*.205);
    float flap=w*((n-.5)*.17+.035*sin(y*9-time*8));
    float blueCurve=-bow+flap,pinkCurve=bow+flap*.7;
    float envelope=1-smoothstep(1.65,3.25,abs(y));
    float u=.14+abs(y)*.225+(n-.5)*.035;
    float2 buv=float2(u,.5+(q.x-blueCurve)/(w*1.85));
    float2 puv=float2(u,.5+(q.x-pinkCurve)/(w*1.85));
    float4 br=plasmaSample(buv,0,9.7);
    float4 pr=plasmaSample(puv,1,19.4);
    float4 bd=plasmaSample(float2(.18+abs(y)*.19,.5+(q.x-blueCurve+w*.25)/(w*2.25)),0,23.4);
    float4 pd=plasmaSample(float2(.18+abs(y)*.19,.5+(q.x-pinkCurve-w*.25)/(w*2.25)),1,5.8);
    float blueSide=1-smoothstep(seamShift-w*.05,seamShift+w*.10,q.x);
    float pinkSide=smoothstep(seamShift-w*.10,seamShift+w*.05,q.x);
    float3 bRGB=br.rgb/max(br.a,.002),pRGB=pr.rgb/max(pr.a,.002);
    float bWhite=smoothstep(.55,.94,min(bRGB.r,min(bRGB.g,bRGB.b)));
    float pWhite=smoothstep(.55,.94,min(pRGB.r,min(pRGB.g,pRGB.b)));
    // The curved material is colored at its hottest surfaces; only the live
    // interface becomes white. This keeps separate blue/pink pressure ownership.
    bRGB=lerp(bRGB,CYAN*.83+BLUE*.17,bWhite*.84);
    pRGB=lerp(pRGB,ROSE*.83+PINK*.17,pWhite*.84);
    float surfaceFlicker=.88+.16*n+.07*micro;
    e.light=(pow(max(bRGB,0),1.28)*br.a*blueSide+
             pow(max(pRGB,0),1.28)*pr.a*pinkSide)*envelope*surfaceFlicker*1.65;
    e.light += (BLUE*bd.a*blueSide+PINK*pd.a*pinkSide)*envelope*(.11+.18*n);
    // Fine counterflow sheets inside the larger bowed curtain give overlap depth.
    float folded=pow(saturate(.5+.5*sin(y*12+abs(q.x)/w*7-time*13+n*5)),5);
    e.light += (CYAN*br.a*blueSide+ROSE*pr.a*pinkSide)*folded*envelope*.26;
    float seamEnvelope=1-smoothstep(1.38,2.44,abs(y));
    float seam=lineMask(abs(q.x-seamShift),max(.62,w*(.028+.014*micro)))*seamEnvelope;
    float seamGlow=gaussian((q.x-seamShift)/(w*.13))*seamEnvelope;
    e.light += float3(1,.95,1)*seam*(2.05+.35*micro);
    e.light += lerp(CYAN,ROSE,smoothstep(-w*.1,w*.1,q.x))*seamGlow*.50;
    e.silhouette=max(max(br.a*blueSide,pr.a*pinkSide)*envelope,seam);
    e.hot=seam;
    e.occlusion=max(br.a*blueSide,pr.a*pinkSide)*envelope*.55;
    e.distortion=max(br.a,pr.a)*envelope*.65;
    // Dense, but spatially separated, ballistic spark layers: small far grains,
    // colored mid-layer dashes, and only a few large white foreground fragments.
    [unroll] for(int i=0;i<28;i++)
    {
        float seed=i*17.371;
        float z=hash11(seed+7);
        float age=frac(time*(.68+z*.70)+hash11(seed+4));
        float side=i%2==0 ? -1.0 : 1.0;
        float horizontal=(hash11(seed+3)-.5)*1.60;
        float2 dir=normalize(float2(horizontal,side));
        float2 trailPoint=dir*w*(.12+age*(1.5+z*3.0));
        trailPoint.x += sin(age*3+seed)*w*.16*age;
        trailPoint.y += age*age*w*.38;
        float2 tail=trailPoint-dir*w*(.015+z*.045+age*.065);
        float sd=segmentDistance(q,tail,trailPoint);
        float spark=lineMask(sd,.32+z*.80)*(1-age)*smoothstep(0,.035,age);
        float3 color=horizontal<0 ? CYAN : ROSE;
        color=lerp(color,float3(.96,.99,1),step(.88,z));
        e.light += color*spark*(.30+z*1.20);
        e.light += color*gaussian(sd/(1.0+z*1.2))*spark*.09;
    }
    return e;
}

Energy pressureClash(float2 p,float2 center,float2 axis,float width,float time,float pulse)
{
    Energy e=zeroEnergy();
    float2 normal=float2(-axis.y,axis.x), rel=p-center;
    float2 q=float2(dot(rel,axis),dot(rel,normal));
    float w=max(width,4.0)*(1+.055*saturate(pulse));
    if(abs(q.x)>w*3.6 || abs(q.y)>w*4.2) return e;
    if(v[9].x>.5) return authoredClash(q,w,time,pulse);
    float y=q.y/w;
    float turbulent=flowNoise(float2(q.x/w*1.7-time*.7,y*2.8-time*2.1));
    float seamDisplacement=w*(.055*sin(y*5.3+time*13.0)+.07*(turbulent-.5));
    float seamX=q.x-seamDisplacement;
    float envelope=1-smoothstep(1.22,2.55,abs(y));
    float curtainExtent=1-smoothstep(1.25,2.85,abs(y));
    float bow=w*(.12+y*y*.34);
    float flap=w*(.08*sin(y*4.4-time*9.1)+.13*(turbulent-.5));
    float blueCurve=-bow+flap;
    float pinkCurve=bow+flap*.7;
    float frontBlue=lineMask(abs(q.x-blueCurve),w*(.10+.05*turbulent))*curtainExtent;
    float frontPink=lineMask(abs(q.x-pinkCurve),w*(.10+.05*turbulent))*curtainExtent;
    float behindBlue=gaussian((q.x-blueCurve)/(w*.48))*curtainExtent;
    float behindPink=gaussian((q.x-pinkCurve)/(w*.48))*curtainExtent;
    float folds=.40+.60*smoothstep(.2,.8,turbulent);
    e.light += BLUE*(behindBlue*.52+frontBlue*.65)*folds;
    e.light += PINK*(behindPink*.52+frontPink*.65)*folds;
    e.light += CYAN*frontBlue*.72+ROSE*frontPink*.72;
    // Filled pressure curtains bridge the hot contact and the outer bow. Advected
    // curved folds keep these surfaces dimensional instead of reading as horns.
    float blueFill=smoothstep(blueCurve-w*.10,blueCurve+w*.16,q.x)*
                   (1-smoothstep(seamDisplacement-w*.04,seamDisplacement+w*.04,q.x))*curtainExtent;
    float pinkFill=(1-smoothstep(pinkCurve-w*.16,pinkCurve+w*.10,q.x))*
                   smoothstep(seamDisplacement-w*.04,seamDisplacement+w*.04,q.x)*curtainExtent;
    float pressureFolds=pow(saturate(.5+.5*sin(y*7.4+abs(q.x)/w*4.0-time*10.0+turbulent*4.0)),3);
    float surfaceDepth=(.20+.42*turbulent+.32*pressureFolds);
    e.light += (BLUE*blueFill+PINK*pinkFill)*surfaceDepth*.57;
    e.light += (CYAN*blueFill+ROSE*pinkFill)*pressureFolds*.21;
    // Opposing fans touch at a thin ragged white seam; there is no central disc.
    float seam=lineMask(abs(seamX),max(w*(.045+.024*turbulent),1.0))*envelope;
    float seamGlow=gaussian(seamX/(w*.20))*envelope;
    e.light += float3(1,.92,1)*seam*(2.2+.55*sin(time*19+y*7));
    e.light += lerp(CYAN,ROSE,smoothstep(-w*.15,w*.15,q.x))*seamGlow*.70;
    // Secondary outward sheets give pressure depth, kept incomplete and much dimmer.
    float spread=frac(time*.80)*w*1.0;
    float secondBlue=lineMask(abs(q.x-blueCurve+spread+w*.20),max(.8,w*.025))*curtainExtent;
    float secondPink=lineMask(abs(q.x-pinkCurve-spread-w*.20),max(.8,w*.025))*curtainExtent;
    float broken=smoothstep(.36,.64,noise(float2(y*4.1,time*1.7)));
    e.light += (CYAN*secondBlue+ROSE*secondPink)*broken*.25;

    // Ballistic sparks are emitted from the seam in near/far layers.
    [unroll] for(int i=0;i<12;i++)
    {
        float seed=i*13.731;
        float age=frac(time*(.70+hash11(seed)*.33)+hash11(seed+4));
        float side=hash11(seed+2)>.5 ? 1.0 : -1.0;
        float horizontal=(hash11(seed+3)-.5)*1.30;
        float2 direction=normalize(float2(horizontal,side));
        float distance=w*(.30+age*(1.5+hash11(seed+5)*2.5));
        float2 sparkPos=direction*distance;
        sparkPos.y += age*age*w*.58;
        float2 tail=sparkPos-direction*w*(.08+.17*age);
        float sd=segmentDistance(q,tail,sparkPos);
        float thick=(i%3==0 ? 1.4 : .72);
        float spark=lineMask(sd,thick)*(1-age)*smoothstep(0,.08,age);
        float3 sc=horizontal<0 ? CYAN : ROSE;
        e.light += sc*spark*(i%3==0 ? 1.65 : .76);
    }
    e.silhouette=max(max(frontBlue,frontPink),seam);
    e.hot=seam;
    e.distortion=max(behindBlue,behindPink)*.8;
    return e;
}

Energy registeredContact(float2 p,float2 center,float2 axis,float2 halfSize,float time)
{
    Energy e=zeroEnergy();
    float2 n=float2(-axis.y,axis.x),r=p-center;
    float2 q=float2(dot(r,axis),dot(r,n));
    halfSize=max(halfSize,float2(2,2));
    float2 s=q/halfSize;
    if(abs(s.x)>1.20 || abs(s.y)>1.20) return e;
    float coarse=flowNoise(float2(s.x*6-time*1.3,s.y*7-time*2.1));
    float fine=noise(float2(s.x*37+time*7,s.y*44-time*15));
    // Preserve the registered user-selected back-curved silhouette. Only the
    // interior receives small, differently timed flows instead of a radial mask.
    float edgeGuard=(1-smoothstep(.80,1.0,abs(s.x)))*(1-smoothstep(.80,1.0,abs(s.y)));
    float2 advection=float2(sin(s.y*17-time*5)*(coarse-.5),
                            sin(s.x*13+time*3)*(fine-.5))*.008*edgeGuard;
    float2 uv=s*.5+.5+advection;
    float4 raw=contactSample(uv,0);
    float4 micro=contactSample(uv+float2(.0019*sin(time*9+s.y*33),.0015*cos(time*7+s.x*29))*edgeGuard,1.35);
    float3 rgb=raw.rgb/max(raw.a,.002);
    float3 microRGB=micro.rgb/max(micro.a,.002);
    float white=smoothstep(.61,.96,min(rgb.r,min(rgb.g,rgb.b)));
    float detailWhite=smoothstep(.70,.98,min(microRGB.r,min(microRGB.g,microRGB.b)));
    float materialPulse=1+.045*sin(time*53+s.y*63)*fine;
    e.light=pow(max(rgb,0),1.34)*raw.a*(1.62+.17*coarse)*materialPulse;
    e.light += float3(.93,.98,1)*(white*raw.a*.55+detailWhite*micro.a*fine*.16);
    // Surface-colored light surrounds the white contact without becoming a disc.
    float seamShift=halfSize.x*(.010*sin(s.y*23-time*17)+.010*(coarse-.5));
    float seam=lineMask(abs(q.x-seamShift),max(.60,halfSize.x*.010))*gaussian(q.y/(halfSize.y*.48));
    e.light += float3(1,.95,1)*seam*.62;
    e.silhouette=raw.a;
    e.hot=max(seam,white*raw.a);
    e.occlusion=raw.a*(.65-.24*white);
    e.distortion=raw.a*(.25+.35*coarse);
    // Small far grains are numerous; only a few front fragments carry a tail.
    [unroll] for(int i=0;i<26;i++)
    {
        float seed=i*19.37;
        float z=hash11(seed+9);
        float age=frac(time*(.61+z*.72)+hash11(seed+4));
        float side=i%2==0 ? -1.0 : 1.0;
        float dx=(hash11(seed+3)-.5)*1.6;
        float2 dir=normalize(float2(dx,side));
        float2 sparkPoint=dir*halfSize.y*(.06+age*(.45+z*.90));
        sparkPoint.x += sin(age*4+seed)*halfSize.x*.08*age;
        sparkPoint.y += age*age*halfSize.y*.10;
        float2 tail=sparkPoint-dir*halfSize.y*(.003+z*.013+age*.014);
        float sd=segmentDistance(q,tail,sparkPoint);
        float spark=lineMask(sd,.26+z*.65)*(1-age)*smoothstep(0,.035,age);
        float3 color=dx<0 ? CYAN : ROSE;
        color=lerp(color,float3(.97,.99,1),step(.88,z));
        e.light += color*spark*(.30+z*1.05);
    }
    return e;
}

Energy forwardPressure(float2 p,float2 start,float2 end,float width,float age,float time)
{
    Energy e=zeroEnergy();
    float2 d=end-start;
    float len=length(d);
    if(len<4) return e;
    float2 axis=d/len,normal=float2(-axis.y,axis.x);
    float2 q=float2(dot(p-start,axis),dot(p-start,normal));
    float w=max(width,3);
    if(q.x < -w || q.x>len+w || abs(q.y)>w*3) return e;
    [unroll] for(int i=0;i<2;i++)
    {
        float travel=frac(age*1.7+i*.5);
        float location=travel*len;
        float spread=1.0+travel*.7;
        float y=q.y/(w*spread);
        float arc=q.x-location+w*.28*y*y;
        float envelope=1-smoothstep(1.35,2.6,abs(y));
        float wave=lineMask(abs(arc),max(w*.055,.8))*envelope;
        float glow=gaussian(arc/(w*.18))*envelope;
        e.light += CYAN*(wave*.65+glow*.13)*(1-travel*.35);
        e.distortion += glow*.35;
    }
    return e;
}

Energy finalExplosion(float2 p,float2 center,float age,float strength,float time,float scale)
{
    Energy e=zeroEnergy();
    float2 q=p-center;
    float radius=(24+210*(1-exp(-max(age,0)*2.4)))*scale;
    float2 ellipse=q/float2(1.0,.76);
    float r=length(ellipse);
    if(r>radius*2.8) return e;
    float a=atan2(ellipse.y,ellipse.x);
    float n=flowNoise(float2(a*3.3,r*.027-time*2.0));
    float cloud=flowNoise(q*.024+float2(-time*1.4,time*.52));
    float foreground=flowNoise(q*.047+float2(time*1.8,-time*1.1));
    float edge=radius*(.82+.15*n+.035*sin(a*9+time*3));
    float shell=gaussian((r-edge)/(radius*(.052+.042*cloud)));
    float plume=saturate(1-r/max(edge,1));
    float depth=sqrt(plume);
    float clouds=smoothstep(.22,.78,cloud*.7+foreground*.3);
    float wisps=pow(saturate(1-abs(cloud-.53)*4.6),2);
    float bright=gaussian(r/max(14*scale,radius*.22));
    float fade=exp(-max(age-.10,0)*.60);
    e.light=(BLUE*(shell*.62+depth*(.20+clouds*.55))+
             CYAN*(shell*(.14+n*.30)+wisps*depth*.32+depth*clouds*.16))*fade;
    e.light += BLUE*gaussian(r/(radius*1.24))*.075*fade;
    e.light += float3(.9,.97,1)*bright*2.2*exp(-age*1.8);
    [unroll] for(int i=0;i<3;i++)
    {
        float ringAge=age-i*.13;
        float rr=(25+ringAge*270)*scale;
        float groundR=length(q/float2(1,.26));
        float ring=lineMask(abs(groundR-rr),max(1.2*scale,4*scale*(1-ringAge*.3)));
        ring *= step(0,ringAge)*exp(-max(ringAge,0)*1.7);
        e.light += CYAN*ring*(.50-i*.10);
    }
    [unroll] for(int j=0;j<8;j++)
    {
        float angle=j*2.399963+hash11(j*3.73)*.35;
        float2 direction=float2(cos(angle),sin(angle)*.73);
        float speed=120+hash11(j*7.19)*160;
        float2 position=direction*(age*speed+22)*scale;
        position.y += age*age*29*scale;
        float2 tail=position-direction*(5+age*8)*scale;
        float spark=lineMask(segmentDistance(q,tail,position),(.8+hash11(j+1)*1.2)*scale);
        e.light += CYAN*spark*exp(-age*1.1)*.85;
    }
    e.silhouette=max(shell,plume);
    e.hot=bright;
    e.distortion=gaussian((r-edge)/(radius*.18))*fade;
    return e;
}

Energy registeredDetonation(float2 p,float2 center,float2 halfSize,float frame,float fade,float time)
{
    Energy e=zeroEnergy();
    float2 s=(p-center)/max(halfSize,float2(2,2));
    if(abs(s.x)>1.04 || abs(s.y)>1.04 || fade<.002)return e;
    float cloud=flowNoise(s*9+float2(-time*.7,time*.9));
    float guard=(1-smoothstep(.82,1,abs(s.x)))*(1-smoothstep(.82,1,abs(s.y)));
    float2 uv=s*.5+.5+float2(sin(s.y*23-time*3),cos(s.x*19+time*2))*(cloud-.5)*.005*guard;
    if(uv.x<0 || uv.x>1 || uv.y<0 || uv.y>1)return e;
    frame=clamp(frame,0,35);float a=floor(frame),b=min(a+1,35);
    float4 raw=lerp(detonationPlasma.SampleLevel(sceneSampler,float3(uv,a),0),
                    detonationPlasma.SampleLevel(sceneSampler,float3(uv,b),0),frac(frame));
    float3 rgb=raw.rgb/max(raw.a,.002);
    float white=smoothstep(.62,.98,min(rgb.r,min(rgb.g,rgb.b)));
    e.light=pow(max(rgb,0),1.18)*raw.a*fade*(1.7+cloud*.25);
    e.light+=float3(.93,.98,1)*white*raw.a*fade*.42;
    e.occlusion=raw.a*fade*.56;
    e.silhouette=raw.a*fade;e.hot=white*raw.a*fade;
    e.distortion=raw.a*fade*.35;
    return e;
}

float4 detailedInkImpact(float2 uv,float4 scene)
{
    float4 rect=v[7];
    float2 local=(uv-rect.xy)/max(rect.zw,float2(.001,.001));
    if(local.x<0 || local.x>1 || local.y<52.0/600.0 || local.y>548.0/600.0)return scene;
    float2 pixels=uv*v[0].xy,center=v[14].xy*v[0].xy;
    float radius=max(v[13].z,2);
    float2 q=(pixels-center)/radius;
    float distance=length(q/float2(1,.76));
    float area=1-smoothstep(1.45,1.95,distance);
    if(area<.001)return scene;
    float age=v[13].w;
    float relief=flowNoise(q*8+float2(age*.45,-age*.6));
    float folds=flowNoise(q*26+float2(age*.8,-age*1.1));
    float grain=noise(q*170+floor(age*60)*.19);
    float2 sampleUV=q*float2(.40,.49)+.5;
    float4 smoke=detonationPlasma.SampleLevel(sceneSampler,float3(saturate(sampleUV),clamp(v[14].z,0,35)),0);
    float material=dot(smoke.rgb/max(smoke.a,.002),float3(.2126,.7152,.0722))*smoke.a;
    float ridges=pow(saturate(1-abs(folds-.52)*4.2),3);
    float edge=pow(saturate(distance/1.1),2.2);
    float luma=dot(scene.rgb,float3(.2126,.7152,.0722));
    float3 color=scene.rgb;
    if(v[13].y>.5)
    {
        // Near-black pressure folds, graphite planes and a cool silver edge.
        // The laser's white negative space stays outside this dark-pixel mask.
        float shadow=1-smoothstep(.08,.32,luma);
        float directional=saturate(.48-q.x*.20-q.y*.26);
        float charcoal=.009+.025*directional+.09*material+.038*relief;
        charcoal+=ridges*(.024+.035*edge)+edge*.036+(grain-.5)*.009;
        color=lerp(color,charcoal.xxx,shadow*area);
    }
    else
    {
        // The inverse frame has pale raised planes and darker cloud crevices,
        // while the surrounding black receives a restrained, uneven falloff.
        float lit=smoothstep(.74,.97,luma);
        float silver=.66+.24*material+.06*relief+.035*ridges+(grain-.5)*.008;
        color=lerp(color,silver.xxx,lit*area);
        float background=1-smoothstep(.025,.07,luma);
        float charcoal=.006+exp(-distance*distance*.7)*(.012+.014*relief);
        float2 start=v[1].xy*v[0].xy,end=center;
        float axis=1-smoothstep(1.3,3.2,segmentDistance(pixels,start,end));
        color=lerp(color,charcoal.xxx,background*area*(1-axis));
    }
    return float4(saturate(color),scene.a);
}

float4 PSMain(VertexOut input) : SV_TARGET
{
    float2 uv=input.uv;
    float4 scene=sceneTex.Sample(sceneSampler,uv);
    if(v[13].x>.5)return detailedInkImpact(uv,scene);
    float mode=v[3].w;
    if(mode<.5 || v[0].w<.001) return scene;
    float2 outputSize=max(v[0].xy,float2(1,1));
    float4 rect=v[7];
    if(rect.z<=0 || rect.w<=0) rect=float4(0,0,1,1);
    float2 sceneUV=(uv-rect.xy)/rect.zw;
    float yMin=mode>2.5 ? 44.0/600.0 : 52.0/600.0;
    if(sceneUV.x<0 || sceneUV.x>1 || sceneUV.y<yMin || sceneUV.y>548.0/600.0) return scene;

    // Evaluate in logical scene pixels so 1080p and 4K have the same fluid detail.
    float scale=max(outputSize.y*rect.w/600.0,.05);
    float2 p=uv*outputSize/scale;
    float2 blueA=v[1].xy*outputSize/scale, blueB=v[1].zw*outputSize/scale;
    float2 pinkA=v[2].xy*outputSize/scale, pinkB=v[2].zw*outputSize/scale;
    float2 clash=v[3].xy*outputSize/scale;
    float blueWidth=max(v[3].z/scale,.5), pinkWidth=max(v[5].y/scale,.5);
    float time=v[0].z;
    float2 blueAxis=normalize(blueB-blueA+float2(.0001,0));
    float2 pinkAxis=normalize(pinkB-pinkA+float2(-.0001,0));
    Energy energy=zeroEnergy();
    float blueAlpha=mode>=2.5 ? saturate(v[5].z) : 0;
    float pinkAlpha=mode>=1.5 ? saturate(v[5].w) : 0;
    if(blueAlpha>.001)
    {
        addEnergy(energy,beam(p,blueA,blueB,blueWidth,time,0,v[8].w),blueAlpha);
        addEnergy(energy,sourceFlare(p,blueA,blueAxis,blueWidth,time,0,0),blueAlpha*.75);
    }
    if(pinkAlpha>.001)
    {
        addEnergy(energy,beam(p,pinkA,pinkB,pinkWidth,time,1,v[8].w),pinkAlpha);
        addEnergy(energy,sourceFlare(p,pinkA,pinkAxis,pinkWidth,time,1,0),pinkAlpha*.75);
        if(mode<2.5)
            addEnergy(energy,sourceFlare(p,pinkB,-pinkAxis,pinkWidth*.48,time,1,0),pinkAlpha*.65);
    }
    if(v[4].z>.001)
        addEnergy(energy,sourceFlare(p,pinkA,pinkAxis,max(pinkWidth,9),time,1,v[4].z),v[4].z);
    if(v[5].x>.001)
        addEnergy(energy,sourceFlare(p,blueA,blueAxis,max(blueWidth,10),time,0,v[5].x),v[5].x);
    if(mode>=2.5 && v[4].y>.001)
    {
        if(v[10].x>.5)
            addEnergy(energy,registeredContact(p,clash,blueAxis,v[10].zw/scale,time),v[4].y);
        else
            addEnergy(energy,pressureClash(p,clash,blueAxis,(blueWidth+pinkWidth)*.56,time,v[8].w),v[4].y);
    }
    if(v[4].w>.001 && blueAlpha>.001)
        addEnergy(energy,forwardPressure(p,blueA,blueB,blueWidth,v[4].x,time),v[4].w);
    if(v[6].w>.001)
    {
        float2 center=v[6].xy*outputSize/scale;
        if(v[11].x>.5)
        {
            addEnergy(energy,registeredDetonation(p,center,v[11].zw/scale,v[11].y,v[12].x,time),1);
            addEnergy(energy,finalExplosion(p,center,v[6].z,v[6].w,time,1),v[6].w*.42);
        }
        else addEnergy(energy,finalExplosion(p,center,v[6].z,v[6].w,time,1),v[6].w);
    }

    float impact=v[8].x;
    if(impact>.5)
    {
        float2 q=p-clash;
        float impactAge=max(v[4].x,0);
        float blastRadius=38+impactAge*48;
        float angle=atan2(q.y,q.x);
        float rr=length(q/float2(1,.72));
        float jagged=1+.09*sin(angle*17)+.05*sin(angle*29);
        float dome=1-smoothstep(blastRadius*jagged-1,blastRadius*jagged+1,rr);
        float edge=lineMask(abs(rr-blastRadius*jagged),1.4);
        float radial=frac(angle*23/PI);
        float hatch=step(.85+.08*saturate((rr/blastRadius-1.0)/.8),radial)*
                    smoothstep(blastRadius,blastRadius*1.12,rr)*
                    (1-smoothstep(blastRadius*1.65,blastRadius*2.0,rr));
        float shape=max(energy.silhouette,max(dome,hatch));
        float hot=max(energy.hot,gaussian(q.x/4.0)*gaussian(q.y/(blastRadius*.9)));
        float luma=dot(scene.rgb,float3(.2126,.7152,.0722));
        float sceneEdge=saturate((abs(ddx(luma))+abs(ddy(luma)))*6);
        float3 ink;
        if(impact<1.5)
        {
            ink=lerp(float3(.96,.97,1),float3(.015,.018,.028),saturate(shape+sceneEdge*.22));
            ink=lerp(ink,float3(1,1,1),hot*.95);
        }
        else if(impact<2.5)
            ink=float3(.003,.006,.014)+float3(.94,.97,1)*saturate(hot+edge*.8+shape*.16+sceneEdge*.1);
        else
            ink=scene.rgb*.12+float3(.04,.07,.10)*energy.hot;
        return float4(ink,scene.a);
    }

    if(v[8].y>.001 && energy.distortion>.01)
    {
        float2 refractDirection=float2(sin(p.y*.052-time*4.0),cos(p.x*.040+time*3.0));
        float2 refractUV=uv+refractDirection*energy.distortion*v[8].y*1.25*scale/outputSize;
        refractUV=clamp(refractUV,rect.xy+float2(0,yMin)*rect.zw,rect.xy+float2(1,548.0/600.0)*rect.zw);
        scene=sceneTex.Sample(sceneSampler,refractUV);
    }
    // Tone-map only emitted energy. The uploaded scene and HUD are never globally exposed.
    float3 emitted=1-exp(-max(energy.light,0)*v[0].w);
    float3 localScene=scene.rgb*(1-saturate(energy.occlusion)*.78);
    float3 composed=localScene+(1-localScene)*emitted;
    return float4(saturate(composed),scene.a);
}
