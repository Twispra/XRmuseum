"""New 7x16 control-section surface. No old shell vertices are an input."""
import numpy as np

def catmull(points, subdivisions=6, closed=False):
    p=np.asarray(points,float);result=[]
    count=len(p) if closed else len(p)-1
    for i in range(count):
        a=p[(i-1)%len(p)] if closed or i else p[0]
        b=p[i];c=p[(i+1)%len(p)];d=p[(i+2)%len(p)] if closed or i+2<len(p) else p[-1]
        for t in np.linspace(0,1,subdivisions,endpoint=False):
            result.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    if not closed:result.append(p[-1])
    return np.asarray(result)

def control_sections(params):
    ys=np.asarray(params['y_sections'])
    controls=[]
    for k,y in enumerate(ys):
        width=params['widths'][k];top=params['tops'][k];bottom=params['bottoms'][k]
        center=(top+bottom)/2
        # Front window shoulders must wrap the independently placed aperture.
        # A symmetric oval centred halfway down the long chin makes the window
        # cutout protrude through its upper side, creating intersecting annulus faces.
        if k>=5:center=params['window_center_z']
        section=[]
        for j in range(16):
            theta=j*np.pi/8;s=np.sin(theta);c=np.cos(theta)
            # Broad, low-curvature transverse shoulders rather than a uniform sphere.
            x=width*np.sign(c)*abs(c)**.82
            z=center+(top-center)*max(s,0)-(center-bottom)*max(-s,0)
            temporal=params.get('temporal_inset',0)*np.exp(-((y+.045)/.075)**2)*np.exp(-((z-.205)/.05)**2)
            x-=np.sign(x)*temporal*abs(c)**2
            yy=y
            if k==6:yy+=params.get('front_plane_slope',0)*(z-params['window_center_z'])
            section.append([x,yy,z])
        controls.append(section)
    return np.asarray(controls)

def surface(params, longitudinal=5, angular=4):
    controls=control_sections(params)
    long=[]
    for j in range(16):long.append(catmull(controls[:,j],longitudinal))
    long=np.stack(long,axis=1)
    rings=np.asarray([catmull(r,angular,closed=True) for r in long])
    return controls,rings

def params_default():
    return {'y_sections':[.170,.135,.085,.025,-.040,-.095,-.124],
            'widths':[.012,.080,.115,.126,.126,.119,.116],
            'tops':[.236,.278,.304,.303,.289,.276,.270],
            'bottoms':[.185,.140,.095,.070,.045,.035,.035],
            'skirt_z0':.101,'skirt_slope':.32,'front_plane_slope':0.,
            'window_center_z':.193,'temporal_inset':0.,'wall_model_m':.0024}
